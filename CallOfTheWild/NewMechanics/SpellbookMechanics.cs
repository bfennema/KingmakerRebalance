﻿using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallOfTheWild.SpellbookMechanics
{

    [AllowedOn(typeof(BlueprintSpellbook))]
    public class NoSpellsPerDaySacaling : BlueprintComponent
    {

    }


    [AllowedOn(typeof(BlueprintSpellbook))]
    public class CompanionSpellbook : BlueprintComponent
    {
        public BlueprintSpellbook spellbook;
    }


    [AllowedOn(typeof(BlueprintSpellbook))]
    public class CanNotUseSpells : BlueprintComponent
    {
       
    }

    [AllowedOn(typeof(BlueprintSpellbook))]
    public class GetKnownSpellsFromMemorizationSpellbook : BlueprintComponent
    {
        public BlueprintSpellbook spellbook;
    }


    //make fake spell book return zero memorized spells
    [Harmony12.HarmonyPatch(typeof(Spellbook))]
    [Harmony12.HarmonyPatch("Rest", Harmony12.MethodType.Normal)]
    class Spellbook__Rest__Patch
    {
        static void Postfix(Spellbook __instance)
        {
            if (!__instance.Blueprint.Spontaneous)
            {
                return;
            }
            var memorization_spellbook_blueprint = __instance.Blueprint.GetComponent<GetKnownSpellsFromMemorizationSpellbook>()?.spellbook;
            if (memorization_spellbook_blueprint == null || memorization_spellbook_blueprint.Spontaneous)
            {
                return;
            }

            var memorization_spellbook = __instance.Owner?.Spellbooks.Where(s => s.Blueprint == memorization_spellbook_blueprint).FirstOrDefault();
            if (memorization_spellbook == null)
            {
                return;
            }

           
            foreach (var s in __instance.GetAllKnownSpells().ToArray())
            {
                __instance.RemoveSpell(s.Blueprint);
            }

            __instance.Owner.Ensure<SpellManipulationMechanics.UnitPartArcanistPreparedMetamagic>().clear();
            var m_known_spells = Helpers.GetField<List<AbilityData>[]>(__instance, "m_KnownSpells");
            var m_known_SpellLevels = Helpers.GetField<Dictionary<BlueprintAbility, int>>(__instance, "m_KnownSpellLevels");
            foreach (var slot in memorization_spellbook.GetAllMemorizedSpells())
            {
                if (slot.Spell != null)
                {
                    var new_ability = new AbilityData(slot.Spell.Blueprint, __instance);
                    if (slot.Spell.MetamagicData != null)
                    {
                        new_ability.MetamagicData = slot.Spell.MetamagicData.Clone();
                        __instance.Owner.Ensure<SpellManipulationMechanics.UnitPartArcanistPreparedMetamagic>().add(new_ability.Blueprint, new_ability.MetamagicData.MetamagicMask);
                    }
                    var spell_level = memorization_spellbook.GetSpellLevel(slot.Spell.Blueprint);
                    new_ability.DecorationBorderNumber = slot.Spell.DecorationBorderNumber;
                    new_ability.DecorationColorNumber = slot.Spell.DecorationColorNumber;
                    
                    m_known_spells[slot.Spell.SpellLevel].Add(new_ability);
                    m_known_SpellLevels[slot.Spell.Blueprint] = spell_level;
                    EventBus.RaiseEvent<ISpellBookCustomSpell>((Action<ISpellBookCustomSpell>)(h => h.AddSpellHandler(new_ability)));
                    //EventBus.RaiseEvent<ILearnSpellHandler>((Action<ILearnSpellHandler>)(h => h.HandleLearnSpell()));
                }
            }
        }
    }


    //make fake spell book return zero memorized spells
    [Harmony12.HarmonyPatch(typeof(Spellbook))]
    [Harmony12.HarmonyPatch("GetMemorizedSpells", Harmony12.MethodType.Normal)]
    class Spellbook__GetMemorizedSpells__Patch
    {
        static void Postfix(Spellbook __instance, int spellLevel, ref IEnumerable<SpellSlot> __result)
        {
            if (__instance.Blueprint.GetComponent<CanNotUseSpells>() != null)
            {
                __result = new SpellSlot[0];
            }
        }
    }


    //add automatic update of companion spellbooks
    [Harmony12.HarmonyPatch(typeof(Spellbook))]
    [Harmony12.HarmonyPatch("AddCasterLevel", Harmony12.MethodType.Normal)]
    class Spellbook__AddCasterLevel__Patch
    {
        static void Postfix(Spellbook __instance)
        {
            foreach (var cs in __instance.Blueprint.GetComponents<CompanionSpellbook>())
            {
                __instance.Owner.DemandSpellbook(cs.spellbook).AddCasterLevel();
            }
        }
    }

    //fix for spellbooks with fixed number of spells per day iindependent of casting stat
    [Harmony12.HarmonyPatch(typeof(Spellbook))]
    [Harmony12.HarmonyPatch("GetSpellsPerDay", Harmony12.MethodType.Normal)]
    class Spellbook__GetSpellsPerDay__Patch
    {
        static bool Prefix(Spellbook __instance, int spellLevel, ref int __result)
        {
            if (__instance.Blueprint.GetComponent<NoSpellsPerDaySacaling>() == null)
            {
                return true;
            }

            ModifiableValueAttributeStat stat = __instance.Owner.Stats.GetStat(__instance.Blueprint.CastingAttribute) as ModifiableValueAttributeStat;
            if (stat == null || stat.ModifiedValue < 10 + spellLevel)
            {
                __result = 0;
                return false;
            }

            int? count = __instance.Blueprint.SpellsPerDay.GetCount(__instance.CasterLevel, spellLevel);
            if (!count.HasValue)
            {
                __result =  0;
            }
            else
            {
                __result = count.Value;
            }
            return false;
        }
    }
}
