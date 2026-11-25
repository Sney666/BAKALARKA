using System;
using System.Collections.Generic;
using RogueCards.Application.Enemies;
using RogueCards.Domain;
namespace RogueCards.Application
{
    public sealed class EnemyBehaviorRegistry
    {
        private readonly Dictionary<EnemyType, IEnemyBehavior> _behaviors = new Dictionary<EnemyType, IEnemyBehavior>();
        public EnemyBehaviorRegistry()
        {
            RegisterDefaults();
        }

        public EnemyBehaviorRegistry(IEnumerable<IEnemyBehavior> behaviors)
        {
            foreach (IEnemyBehavior behavior in behaviors)
            {
                Register(behavior);
            }
        }

        public void Register(IEnemyBehavior behavior)
        {
            _behaviors[behavior.EnemyType] = behavior;
        }

        public bool HasBehavior(EnemyType type)
        {
            return _behaviors.ContainsKey(type);
        }

        public bool TryGetBehavior(EnemyType type, out IEnemyBehavior? behavior)
        {
            return _behaviors.TryGetValue(type, out behavior);
        }

        public IEnemyBehavior GetBehavior(EnemyType type)
        {
            if (_behaviors.TryGetValue(type, out IEnemyBehavior? behavior))
            {
                return behavior;
            }

            throw new InvalidOperationException($"No enemy behavior is registered for '{type}'.");
        }

        private void RegisterDefaults()
        {
            Register(new PawnBehavior());
            Register(new ArcherBehavior());
            Register(new SpearmanBehavior());
            Register(new ShamanBehavior());
            Register(new VikingChiefBehavior());
        }
    }
}