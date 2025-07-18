//All resettable objects must implement this such that a level restart can put them in their initial state
public interface ILevelResettable
{
    void ResetToInitialState();
    void SetInitialState();
}
