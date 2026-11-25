namespace RogueCards.Domain
{
    public enum ScreenMode
    {
        Menu,
        Arena,
        Stats,
        GameOver
    }
    public enum WeaponCategory
    {
        Melee,
        Ranged,
        Special,
        Movement
    }

    public enum TargetingType
    {
        Orthogonal,
        Diagonal,
        Self
    }

    public enum WeaponEffectType
    {
        VikingAxe,
        WoodenClub,
        TwoHandedHammer,
        ThrowingAxe,
        HuntingBow,
        HeavySpear,
        BearTrap,
        BoatHook,
        DiagonalDash,
        Slide
    }

    public enum EnemyType
    {
        Pawn,
        Archer,
        Spearman,
        Shaman,
        VikingChief
    }

    public enum EnemyState
    {
        Unarmed,
        Armed,
        Charging
    }

    public enum HazardType
    {
        BearTrap,
        PoisonPool
    }

    public enum RewardType
    {
        None,
        StartingDraft,
        PickCard,
        Upgrade
    }

    public enum UpgradeModifierType
    {
        PlusOneDamage,
        PlusTwoDamage,
        MinusOneCooldown,
        MinusTwoCooldown,
        Poison
    }

    public enum PermanentUpgradeType
    {
        DurabilityTraining,
        SwiftBoots,
        Charisma,
        MeleeTraining,
        TacticalMaster,
        RangedTraining,
        SpecialityMaster
    }

    public enum CharacterSpecialType
    {
        VikingScream,
        IronBulwark
    }
}