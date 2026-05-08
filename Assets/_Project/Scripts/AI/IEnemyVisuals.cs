namespace Steading.AI
{
    // Visual contract used by EnemyController. Both the old procedural-blob
    // rig (EnemyVisualAnimator) and the new Mecanim-driven bridge
    // (EnemyAnimatorBridge) implement it so EnemyController doesn't care
    // which visual is attached.
    public interface IEnemyVisuals
    {
        void EnsureRig();
        void PlayAttack(int variant);
        void PlayStagger(float seconds);
    }
}
