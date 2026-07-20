namespace Narthex.Gameplay
{
    public sealed class AttackComboTracker
    {
        private readonly float comboWindowSeconds;
        private float previousAttackTime = float.NegativeInfinity;

        public AttackComboTracker(float comboWindowSeconds)
        {
            this.comboWindowSeconds = UnityEngine.Mathf.Max(0.01f, comboWindowSeconds);
        }

        public int CurrentStage { get; private set; }

        public int RegisterAttack(float attackTime)
        {
            var continuesCombo = CurrentStage > 0 && CurrentStage < 3 &&
                                 attackTime - previousAttackTime <= comboWindowSeconds;
            CurrentStage = continuesCombo ? CurrentStage + 1 : 1;
            previousAttackTime = attackTime;
            return CurrentStage;
        }

        public void Reset()
        {
            CurrentStage = 0;
            previousAttackTime = float.NegativeInfinity;
        }
    }
}
