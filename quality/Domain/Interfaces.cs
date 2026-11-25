using System.Collections.Generic;
namespace RogueCards.Domain
{
    public interface IGameDataRepository
    {
        GameDatabase LoadAll();
        IReadOnlyList<string> ParseErrors { get; }
    }

    public interface IRunPersistence
    {
        bool HasActiveRunSave();

        PlayerProfile LoadProfile(GameDatabase db, out string warning);

        void SaveProfile(PlayerProfile profile);

        bool SaveActiveRun(ActiveRunState run);

        bool LoadActiveRun(GameDatabase db, out ActiveRunState? run, out string error);

        void ClearActiveRunSave();

        GameStatistics LoadStatistics();

        void SaveStatistics(GameStatistics stats);
    }

    public interface ICombatLogger
    {
        string StartNewRunLog();

        void Append(string logPath, string message);
    }

    public interface ICardEffect
    {
        WeaponEffectType EffectType { get; }

        AimPreviewResult ComputePreview(ActiveRunState run, CardInstance card, GridPos direction);

        void Execute(ICombatContext context, CardInstance card, GridPos direction, int damage);
    }

    public interface IEnemyBehavior
    {
        EnemyType EnemyType { get; }

        void DecideAction(ICombatContext context, EnemyInstance enemy);

        void ExecuteChargedAttack(ICombatContext context, EnemyInstance enemy);
    }

    public interface ICombatContext
    {
        ActiveRunState Run { get; }

        GameDatabase Database { get; }

        void DamageEnemy(EnemyInstance enemy, int damage, bool applyPoison, string source);

        void DamageEnemyByFriendlyFire(EnemyInstance attacker, EnemyInstance victim, int damage, string source);

        void DamagePlayer(EnemyInstance? attacker, int damage, string source);

        void TriggerHazardsForEnemy(EnemyInstance enemy);

        void TriggerHazardsForPlayer();

        void ArmBearTrapAt(GridPos pos);

        EnemyInstance? SpawnEnemy(EnemyType type);

        void Log(string message);
    }
}