using System;
using System.IO;
using System.Windows.Forms;
using RogueCards.Application;
using RogueCards.Domain;
using RogueCards.Persistence;
using RogueCards.Presentation;
namespace RogueCards
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            string baseDir = AppContext.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            string savesDir = Path.Combine(baseDir, "Saves");
            string logsDir = Path.Combine(baseDir, "Logs");

            DefaultDataSeeder seeder = new DefaultDataSeeder();
            IGameDataRepository repository = new TextGameDataRepository(dataDir, savesDir, logsDir, seeder);
            IRunPersistence persistence = new TextRunPersistence(savesDir);
            ICombatLogger combatLogger = new FileCombatLogger(logsDir);

            CardEffectRegistry cardEffects = new CardEffectRegistry();
            EnemyBehaviorRegistry enemyBehaviors = new EnemyBehaviorRegistry();
            RewardService rewardService = new RewardService();

            GameEngine engine = new GameEngine(
                repository,
                persistence,
                combatLogger,
                cardEffects,
                enemyBehaviors,
                rewardService);

            System.Windows.Forms.Application.Run(new MainForm(engine));
        }
    }
}