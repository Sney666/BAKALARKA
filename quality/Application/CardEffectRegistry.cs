using System;
using System.Collections.Generic;
using RogueCards.Application.Cards;
using RogueCards.Domain;
namespace RogueCards.Application
{
    public sealed class CardEffectRegistry
    {
        private readonly Dictionary<WeaponEffectType, ICardEffect> _effects = new Dictionary<WeaponEffectType, ICardEffect>();
        public CardEffectRegistry()
        {
            RegisterDefaults();
        }

        public CardEffectRegistry(IEnumerable<ICardEffect> effects)
        {
            foreach (ICardEffect effect in effects)
            {
                Register(effect);
            }
        }

        public void Register(ICardEffect effect)
        {
            _effects[effect.EffectType] = effect;
        }

        public bool HasEffect(WeaponEffectType type)
        {
            return _effects.ContainsKey(type);
        }

        public bool TryGetEffect(WeaponEffectType type, out ICardEffect? effect)
        {
            return _effects.TryGetValue(type, out effect);
        }

        public ICardEffect GetEffect(WeaponEffectType type)
        {
            if (_effects.TryGetValue(type, out ICardEffect? effect))
            {
                return effect;
            }

            throw new InvalidOperationException($"No card effect is registered for '{type}'.");
        }

        private void RegisterDefaults()
        {
            Register(new SingleAdjacentEffect());
            Register(new KnockbackAdjacentEffect());
            Register(new ArcSweepEffect());
            Register(new LineProjectileEffect(WeaponEffectType.ThrowingAxe, TargetingType.Orthogonal));
            Register(new LineProjectileEffect(WeaponEffectType.HuntingBow, TargetingType.Diagonal));
            Register(new PierceTwoLineEffect());
            Register(new PlaceBearTrapEffect());
            Register(new HarpoonPullEffect());
            Register(new DiagonalDashEffect());
            Register(new SlideTwoEffect());
        }
    }
}