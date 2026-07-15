namespace Narthex.Gameplay
{
    public readonly struct HitConfirmed
    {
        public readonly string AttackerId;
        public readonly string TargetId;
        public readonly string HitboxId;
        public readonly int Damage;
        public HitConfirmed(string attackerId, string targetId, string hitboxId, int damage)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            HitboxId = hitboxId;
            Damage = damage;
        }
    }

    public readonly struct PlayerHit
    {
        public readonly string PlayerId;
        public readonly int Damage;
        public PlayerHit(string playerId, int damage) { PlayerId = playerId; Damage = damage; }
    }

    public readonly struct EnemyKilled
    {
        public readonly string EnemyId;
        public readonly string StageId;
        public EnemyKilled(string enemyId, string stageId) { EnemyId = enemyId; StageId = stageId; }
    }
}
