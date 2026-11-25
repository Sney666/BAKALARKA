using System.Text;

namespace RogueCards.Persistence
{
    public sealed class DefaultDataSeeder
    {
        public void EnsureWorkspaceDirectories(string dataDir, string savesDir, string logsDir)
        {
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(savesDir);
            Directory.CreateDirectory(logsDir);
        }
        public void EnsureDefaultFilesExist(string dataDir)
        {
            Directory.CreateDirectory(dataDir);

            WriteIfMissing(Path.Combine(dataDir, "characters.txt"), BuildCharactersContent());
            WriteIfMissing(Path.Combine(dataDir, "weapons.txt"), BuildWeaponsContent());
            WriteIfMissing(Path.Combine(dataDir, "enemies.txt"), BuildEnemiesContent());
            WriteIfMissing(Path.Combine(dataDir, "waves.txt"), BuildWavesContent());
            WriteIfMissing(Path.Combine(dataDir, "upgrades.txt"), BuildUpgradesContent());
            WriteIfMissing(Path.Combine(dataDir, "achievements.txt"), BuildAchievementsContent());
        }

        private static void WriteIfMissing(string filePath, string content)
        {
            if (File.Exists(filePath))
            {
                return;
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        private static string BuildCharactersContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Playable Characters");
            sb.AppendLine("# Format: Id|Name|BaseMaxHp|SpecialName|SpecialType|SpecialCooldown|IsDefaultUnlocked|UnlockAchievementId|Description");
            sb.AppendLine("viking|Viking|5|Viking Scream|VikingScream|6|true||Knockback all 8 adjacent enemies by 1 tile. Deals 0 dmg (1 dmg with Speciality Master).");
            sb.AppendLine("shieldmaiden|Shieldmaiden|5|Iron Bulwark|IronBulwark|6|false|kingslayer|1 turn full damage immunity and -1 CD to equipped cards (+1 dmg to orthogonal adjacent enemies with Speciality Master).");
            return sb.ToString();
        }

        private static string BuildWeaponsContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Weapons and Cards");
            sb.AppendLine("# Format: Id|Name|Category|BaseDamage|BaseCooldown|Range|Targeting|EffectType|UnlockCost|UnlockOrder|IsDefaultUnlocked|Description");
            sb.AppendLine("viking_axe|Viking Axe|Melee|3|4|1|Orthogonal|VikingAxe|0|0|true|Strike an orthogonally adjacent tile for 3 damage.");
            sb.AppendLine("wooden_club|Wooden Club|Melee|2|5|1|Orthogonal|WoodenClub|15|1|false|Strike an orthogonally adjacent tile for 2 damage and knock the enemy back 1 tile.");
            sb.AppendLine("two_handed_hammer|Two-handed Hammer|Melee|4|6|1|Orthogonal|TwoHandedHammer|30|5|false|Swing in a 3-tile orthogonal arc dealing 4 damage to every enemy hit.");
            sb.AppendLine("throwing_axe|Throwing Axe|Ranged|2|3|5|Orthogonal|ThrowingAxe|0|0|true|Throw orthogonally up to 5 tiles, striking the first enemy for 2 damage.");
            sb.AppendLine("hunting_bow|Hunting Bow|Ranged|3|5|5|Diagonal|HuntingBow|20|2|false|Fire diagonally up to 5 tiles, striking the first enemy for 3 damage.");
            sb.AppendLine("heavy_spear|Heavy Spear|Ranged|4|6|5|Orthogonal|HeavySpear|35|6|false|Hurl orthogonally up to 5 tiles, piercing up to 2 enemies for 4 damage.");
            sb.AppendLine("bear_trap|Bear Trap|Special|2|6|0|Self|BearTrap|0|0|true|Place a trap on your tile. Arms when you move off; deals 2 damage and stuns enemies.");
            sb.AppendLine("boat_hook|Boat Hook|Special|1|5|3|Orthogonal|BoatHook|25|3|false|Hook the first enemy within 3 orthogonal tiles for 1 damage and pull them adjacent.");
            sb.AppendLine("diagonal_dash|Diagonal Dash|Movement|0|4|1|Diagonal|DiagonalDash|0|0|true|Dash 1 tile diagonally onto an unoccupied space.");
            sb.AppendLine("slide|Slide|Movement|0|5|2|Orthogonal|Slide|25|4|false|Slide up to 2 tiles orthogonally through unoccupied spaces.");
            return sb.ToString();
        }

        private static string BuildEnemiesContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Enemies");
            sb.AppendLine("# Format: Type|Name|MaxHp|Damage|Range|CoinReward|SpawnOnEdge|WeaponLabel|Description");
            sb.AppendLine("Pawn|Pawn|3|2|1|2|false|Axe|Moves orthogonally toward the player and charges a melee axe strike at range 1.");
            sb.AppendLine("Archer|Archer|2|2|5|3|true|Bow|Aligns row or column at range > 2 to fire; retreats if the player closes in.");
            sb.AppendLine("Spearman|Spearman|4|2|2|3|false|Spear|Advances toward the player and charges a 2-tile orthogonal spear thrust.");
            sb.AppendLine("Shaman|Shaman|3|1|3|4|true|Potion|Keeps distance 3 and throws a poison potion creating a 3-turn toxic pool.");
            sb.AppendLine("VikingChief|Viking Chief|30|3|5|30|false|Dual Arms|Wave 10 Boss cycling Throwing Axe, Hook and Club with summons, and Evolved Viking Axe.");
            return sb.ToString();
        }

        private static string BuildWavesContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Level 1 Waves");
            sb.AppendLine("# Format: WaveNumber|EnemyType:Count,EnemyType:Count|RewardType");
            sb.AppendLine("1|Pawn:1|None");
            sb.AppendLine("2|Pawn:1,Archer:1|PickCard");
            sb.AppendLine("3|Spearman:1,Pawn:1|None");
            sb.AppendLine("4|Spearman:1,Archer:1|Upgrade");
            sb.AppendLine("5|Pawn:2,Shaman:1|None");
            sb.AppendLine("6|Spearman:1,Archer:2|PickCard");
            sb.AppendLine("7|Pawn:3,Archer:2|None");
            sb.AppendLine("8|Pawn:2,Spearman:1,Shaman:1|Upgrade");
            sb.AppendLine("9|Pawn:1,Shaman:1,Spearman:1,Archer:2|None");
            sb.AppendLine("10|VikingChief:1|None");
            return sb.ToString();
        }

        private static string BuildUpgradesContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Permanent Upgrades");
            sb.AppendLine("# Format: Id|Name|UpgradeType|Cost|UnlockOrder|Description");
            sb.AppendLine("durability_training_1|Durability Training I|DurabilityTraining|20|1|+1 max HP at the start of every run.");
            sb.AppendLine("swift_boots|Swift Boots|SwiftBoots|30|2|-1 cooldown on all Movement cards.");
            sb.AppendLine("charisma|Charisma|Charisma|40|3|+1 free reward reroll per run.");
            sb.AppendLine("melee_training_1|Melee Training I|MeleeTraining|50|4|+1 damage on all Melee cards.");
            sb.AppendLine("tactical_master|Tactical Master|TacticalMaster|60|5|Wait reduces cooldown of equipped cards by 1 extra turn.");
            sb.AppendLine("ranged_training_1|Ranged Training I|RangedTraining|70|6|+1 damage on all Ranged cards.");
            sb.AppendLine("speciality_master|Speciality Master|SpecialityMaster|80|7|Character Special deals 1 damage.");
            return sb.ToString();
        }

        private static string BuildAchievementsContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Achievements");
            sb.AppendLine("# Format: Id|Name|Description|TargetValue|CoinReward|UnlockCharacterId");
            sb.AppendLine("first_blood|First Blood|Defeat 1 enemy.|1|10|");
            sb.AppendLine("wave_survivor|Wave Survivor|Reach Wave 5.|5|15|");
            sb.AppendLine("friendly_betrayal|Friendly Betrayal|Defeat 1 enemy with friendly fire.|1|20|");
            sb.AppendLine("trap_master|Trap Master|Trigger 5 Bear Trap stuns across runs.|5|20|");
            sb.AppendLine("combo_striker|Combo Striker|Play 3 or more equipped cards in 1 turn.|3|25|");
            sb.AppendLine("untouchable|Untouchable|Clear a wave with 0 damage taken.|1|25|");
            sb.AppendLine("well_equipped|Well Equipped|Unlock 3 or more permanent upgrades or weapons in the Menu.|3|30|");
            sb.AppendLine("veteran_slayer|Veteran Slayer|Defeat 50 enemies total.|50|35|");
            sb.AppendLine("no_second_chances|No Second Chances|Win a 10-wave run with 0 rerolls.|1|40|");
            sb.AppendLine("kingslayer|Kingslayer|Defeat the Viking Chief on Wave 10.|1|50|shieldmaiden");
            return sb.ToString();
        }
    }
}