import copy
import unittest
import tempfile
import signal
from pathlib import Path
from unittest.mock import Mock, patch, call
from audio_process_pause_check import Unavailable, assess_window, node_identity, profile_rows, run_trial


def rows(injected=False):
    return [dict(sequence=i, signalNs=i*5_000_000, driverXruns=0,
                 nodeXruns=max(0,min(i-201,49)) if injected else 0) for i in range(401)]


class ProcessPauseTests(unittest.TestCase):
    def test_external_counter_detects_pause_and_baseline_is_clean(self):
        self.assertEqual('detected',assess_window(rows(True),1_000_000_000,1_250_000_000,True)['status'])
        self.assertEqual('clean',assess_window(rows(),1_000_000_000,1_250_000_000,False)['status'])

    def test_silent_counter_does_not_pass_injection(self):
        self.assertFalse(assess_window(rows(),1_000_000_000,1_250_000_000,True)['passed'])

    def test_driver_error_does_not_substitute_for_client_error(self):
        data=rows()
        for r in data[205:]:r['driverXruns']=1
        self.assertFalse(assess_window(data,1_000_000_000,1_250_000_000,True)['passed'])

    def test_out_of_window_increment_fails_attribution(self):
        data=rows(True)
        for r in data[150:]:r['nodeXruns']+=1
        self.assertFalse(assess_window(data,1_000_000_000,1_250_000_000,True)['passed'])

    def test_missing_cycle_follower_or_counter_reset_is_unknown(self):
        for kind in ['cycle','follower','reset','time']:
            data=rows(True)
            if kind=='cycle':del data[225]
            if kind=='follower':data[225]['nodeXruns']=None
            if kind=='reset':data[225]['nodeXruns']=0
            if kind=='time':data[225]['signalNs']=data[224]['signalNs']-1
            with self.subTest(kind=kind),self.assertRaises(Unavailable):
                assess_window(data,1_000_000_000,1_250_000_000,True)

    def test_complete_and_incomplete_notifications_can_share_timestamp(self):
        data=rows(True)
        data[225]['signalNs']=data[224]['signalNs']
        self.assertTrue(assess_window(data,1_000_000_000,1_250_000_000,True)['passed'])

    def test_quantum_outside_window_is_ignored_but_inside_is_unknown(self):
        data=rows()
        data[0]['quantum']=2048
        data[-1]['quantum']=2048
        self.assertTrue(assess_window(data,1_000_000_000,1_250_000_000,False)['passed'])
        data[225]['quantum']=2048
        with self.assertRaises(Unavailable):
            assess_window(data,1_000_000_000,1_250_000_000,False)

    def test_missing_bracketing_samples_is_unknown(self):
        with self.assertRaises(Unavailable):
            assess_window(rows()[190:260],1_000_000_000,1_250_000_000,True)

    def test_parser_requires_complete_stream_and_matching_driver(self):
        target={'driverId':1,'driverName':'driver','nodeId':2,'nodeName':'probe'}
        data=[{'type':'info','count':5},
              {'type':'clock','rate':'1/48000','duration':256},
              {'type':'driver','id':1,'name':'driver','signal':100,'xrun_count':3},
              {'type':'follower','id':2,'name':'probe','xrun_count':7},{}]
        parsed=profile_rows(data,target)
        self.assertEqual(7,parsed[0]['nodeXruns'])
        self.assertEqual(3,parsed[0]['driverXruns'])
        for bad in [data[:-1],data[:2]+data[3:],[]]:
            with self.assertRaises(Unavailable):profile_rows(bad,target)
        other=copy.deepcopy(data);other[3]['name']='reused-node'
        with self.assertRaises(Unavailable):profile_rows(other,target)
        other=copy.deepcopy(data);other[1]['duration']=128
        with self.assertRaises(Unavailable):profile_rows(other,target)

    def test_interruption_after_stop_resumes_and_reaps_owned_processes(self):
        observer=Mock();child=Mock(pid=12345)
        observer.poll.return_value=child.poll.return_value=None
        with tempfile.TemporaryDirectory() as directory, \
                patch('audio_process_pause_check.subprocess.Popen', side_effect=[observer,child]), \
                patch('audio_process_pause_check.read_registry', return_value=[]), \
                patch('audio_process_pause_check.node_identity', return_value={}), \
                patch('audio_process_pause_check.time.sleep'), \
                patch('audio_process_pause_check.os.killpg') as kill_group, \
                patch.object(Path,'read_text',side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                run_trial(Path('/fake/probe'),Path(directory)/'trial',True)
        self.assertEqual([call(signal.SIGSTOP),call(signal.SIGCONT)],child.send_signal.call_args_list)
        kill_group.assert_called_once_with(child.pid, signal.SIGKILL)
        child.wait.assert_called_once()
        observer.terminate.assert_called_once()
        observer.wait.assert_called_once()

    def test_scheduler_is_reaped_when_probe_startup_fails(self):
        observer=Mock();child=Mock(pid=12345);sampler=Mock()
        child.poll.return_value=1
        observer.poll.return_value=sampler.poll.return_value=None
        with tempfile.TemporaryDirectory() as directory, \
                patch('audio_process_pause_check.subprocess.Popen',side_effect=[observer,child,sampler]), \
                patch('audio_process_pause_check.os.killpg') as kill_group:
            with self.assertRaises(Unavailable):
                run_trial(Path('/fake/probe'),Path(directory)/'trial',False,scheduler=True)
        kill_group.assert_called_once_with(child.pid,signal.SIGKILL)
        sampler.terminate.assert_called_once()
        sampler.wait.assert_called_once()
        observer.terminate.assert_called_once()

    def test_pid_ownership_and_serials_not_display_name(self):
        registry=[{'id':1,'type':'PipeWire:Interface:Client','info':{'props':{'application.process.id':99}}},
                  {'id':2,'type':'PipeWire:Interface:Node','info':{'props':{
                      'client.id':1,'media.class':'Stream/Output/Audio','node.driver-id':3,'node.name':'probe','object.serial':20}}},
                  {'id':3,'type':'PipeWire:Interface:Node','info':{'props':{'node.name':'driver','object.serial':30}}}]
        self.assertEqual(20,node_identity(registry,99)['nodeSerial'])
        with self.assertRaises(Unavailable):node_identity(registry,100)
        duplicate=copy.deepcopy(registry[1]);duplicate['id']=4
        with self.assertRaises(Unavailable):node_identity(registry+[duplicate],99)


if __name__=='__main__':unittest.main()
