using System;

namespace Inceptum.Raft
{
    class ActionDisposable:IDisposable
    {
        private readonly Action m_Action;

        private ActionDisposable(Action action)
        {
            m_Action = action;
        }

        public static IDisposable Create(Action action)
        {
            return new ActionDisposable(action);
        }
        public void Dispose()
        {
            m_Action();
        }
    }
}