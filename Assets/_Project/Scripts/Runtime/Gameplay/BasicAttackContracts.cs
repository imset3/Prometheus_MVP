namespace Narthex.Gameplay
{
    public readonly struct BasicAttackRequest
    {
        public readonly string WeaponId;

        public BasicAttackRequest(string weaponId)
        {
            WeaponId = weaponId;
        }
    }
}
