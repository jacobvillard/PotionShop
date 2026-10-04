using UnityEngine;

public interface ISinkable
{
    bool CanBeRemovedFromSink { get; }
    bool IsEmpty { get; }
    bool CanLockInSink { get; }

    void SetIsHeld(bool isHeld);
    void SetSink(Sink sink);
    void LockToSink(UnityEngine.Transform lockPoint);
    void UnlockFromSink();

    bool CanReceiveLiquid(LiquidIngredient liquid);
    void AddLiquid(LiquidIngredient liquid, float amount);

    bool IsFull { get; }
}

public interface IHoldable
{
    void SetIsHeld(bool isHeld);
}
