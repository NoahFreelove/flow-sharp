using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ActionHistoryTests
{
    private sealed class Action : IUndoableAction
    {
        public string Description => "edit";
        public int Value;
        public bool FailUndo, FailRedo;
        public void Undo() { if (FailUndo) throw new InvalidOperationException(); Value--; }
        public void Redo() { if (FailRedo) throw new InvalidOperationException(); Value++; }
    }

    [Fact]
    public void FailedActionsDoNotMoveStacksOrClearRedoBranch()
    {
        var history = new ActionHistory();
        var action = new Action();
        history.Execute(action, 10);
        action.FailUndo = true;
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        action.FailUndo = false;
        history.Undo();
        action.FailRedo = true;
        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(1, history.RedoCount);
        Assert.Throws<InvalidOperationException>(() => history.Execute(new Action { FailRedo = true }, 10));
        Assert.Equal(1, history.RedoCount);
        Assert.False(history.IsDirty);
        history.Execute(new Action(), 10);
        Assert.Equal(0, history.RedoCount);
        Assert.True(history.IsDirty);
    }

    [Fact]
    public void MemoryAndEntryLimitsEvictOldestUndoWithoutLosingSaveIdentity()
    {
        var history = new ActionHistory(2, 25);
        history.Execute(new Action(), 10);
        history.MarkSaved();
        history.Execute(new Action(), 10);
        history.Execute(new Action(), 10);
        Assert.Equal(20, history.RetainedBytes);
        Assert.Equal(2, history.UndoCount);
        history.Undo();
        history.Undo();
        Assert.False(history.IsDirty);
        Assert.False(history.Undo());
        Assert.Equal(20, history.RetainedBytes);
        var oversized = new Action();
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Execute(oversized, 26));
        Assert.Equal(0, oversized.Value);
        Assert.Equal(2, history.RedoCount);
    }

    [Fact]
    public void NewBranchCannotAccidentallyMatchSavedRevision()
    {
        var history = new ActionHistory();
        history.Execute(new Action(), 1);
        history.MarkSaved();
        history.Undo();
        history.Execute(new Action(), 1);
        Assert.True(history.IsDirty);
    }
    [Fact]
    public void CompositeRollsBackFailedRedoAndUndo()
    {
        var first = new Action();
        var second = new Action { FailRedo = true };
        var composite = new CompositeAction("group", [first, second]);
        var history = new ActionHistory();
        Assert.Throws<InvalidOperationException>(() => history.Execute(composite, 20));
        Assert.Equal(0, first.Value);
        Assert.Equal(0, second.Value);
        Assert.Equal(0, history.UndoCount);
        second.FailRedo = false;
        history.Execute(composite, 20);
        first.FailUndo = true;
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Equal(1, first.Value);
        Assert.Equal(1, second.Value);
        Assert.Equal(1, history.UndoCount);
        first.FailUndo = false;
        Assert.True(history.Undo());
        Assert.Equal(0, first.Value);
        Assert.Equal(0, second.Value);
    }

    [Fact]
    public void MaximumBudgetDoesNotOverflowAccounting()
    {
        var history = new ActionHistory(5, long.MaxValue);
        history.Execute(new Action(), long.MaxValue - 1);
        history.Execute(new Action(), 2);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(2, history.RetainedBytes);
    }
}
