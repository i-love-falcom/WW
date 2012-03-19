using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WW
{
    public abstract class IFSMInterface
    {
        public abstract void Entry(object context);
        public abstract bool Execute(object context);
        public abstract void Exit(object context);
    }

    // 有限状態機械の基底クラス
    public class FSM
    {
        private Stack<IFSMInterface> currentState = new Stack<IFSMInterface>();

        private class BeginState : IFSMInterface
        {
            public override void Entry(object context) { }
            public override bool Execute(object context) { return true; }
            public override void Exit(object context) { }
        }

        public FSM()
        {
            currentState.Push(new BeginState());
        }

        public FSM(IFSMInterface initialState)
        {
            currentState.Push(initialState);
        }

        // 更新
        public void ExecuteState(object context)
        {
            IFSMInterface state = currentState.Peek();
            if (!state.Execute(context))
            {
                PopState(context);
            }
        }

        // 状態変更
        public void ChangeState(IFSMInterface state, object context)
        {
            PopState(context);
            PushState(state, context);
        }

        public void PushState(IFSMInterface subState, object context)
        {
            subState.Entry(context);
            currentState.Push(subState);
        }

        public void PopState(object context)
        {
            IFSMInterface subState = currentState.Pop();
            subState.Exit(context);
        }
    }
}
