namespace Inceptum.Raft
{
    public interface IStateMachine<in TCommand>
    {
        void Apply(TCommand command);
    }
}