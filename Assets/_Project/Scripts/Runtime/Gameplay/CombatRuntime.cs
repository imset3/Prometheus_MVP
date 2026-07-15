namespace Narthex.Gameplay
{
    public enum CombatActorKind { Player, Enemy, Boss }
    public enum CombatState { Idle, Move, Attack, Dash, Hit, Stun, Invincible, Dead }

    public class ActorRuntimeState
    {
        public string ActorId { get; }
        public CombatActorKind Kind { get; }
        public int MaxHealth { get; }
        public int CurrentHealth { get; internal set; }
        public CombatState State { get; internal set; } = CombatState.Idle;
        public bool IsInvincible { get; set; }
        public string StageId { get; set; }
        public string UnlockTreeId { get; set; }
        public bool IsAlive => State != CombatState.Dead;

        public ActorRuntimeState(string actorId, CombatActorKind kind, int maxHealth)
        {
            ActorId = actorId;
            Kind = kind;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }
    }

    public sealed class PlayerRuntimeState : ActorRuntimeState
    {
        public int HitCount { get; internal set; }

        public PlayerRuntimeState(string playerId, int maxHealth) : base(playerId, CombatActorKind.Player, maxHealth) { }
    }

    public readonly struct DamagePacket
    {
        public readonly string SourceId;
        public readonly string HitboxId;
        public readonly int Damage;

        public DamagePacket(string sourceId, string hitboxId, int damage)
        {
            SourceId = sourceId;
            HitboxId = hitboxId;
            Damage = damage;
        }
    }
}
