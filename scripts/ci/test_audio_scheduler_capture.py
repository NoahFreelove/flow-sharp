import tempfile
import unittest
from pathlib import Path
from audio_scheduler_capture import selected_paths


class SelectionTests(unittest.TestCase):
    def test_missing_identity_waits_without_falling_back_to_all_threads(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            self.assertEqual([],selected_paths(root,root/'identity',12))

    def test_selects_only_the_owned_callback(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);identity=root/'identity';identity.write_text('12 34\n')
            self.assertEqual([root/'task/34'],selected_paths(root,identity,12))

    def test_foreign_malformed_or_nonpositive_identity_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);identity=root/'identity'
            for text in ['13 34','12 0','12 -1','12','12 34 extra']:
                identity.write_text(text)
                with self.subTest(text=text),self.assertRaises(ValueError):
                    selected_paths(root,identity,12)
