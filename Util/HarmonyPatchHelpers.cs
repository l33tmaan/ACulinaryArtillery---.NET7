using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ACulinaryArtillery.Util {
    public static class CodeMatcherExtensions {

        /// <summary>
        /// Extracts the current position out of a <see cref="CodeMatcher"/>.
        /// </summary>
        public static CodeMatcher RememberPositionIn(this CodeMatcher matcher, out int position) {
            position = matcher.Pos;
            return matcher;
        }

        /// <summary>
        /// Extracts the <see cref="CodeMatcher.NamedMatch(string)"/> out of a <see cref="CodeMatcher"/>.
        /// </summary>
        public static CodeMatcher RememberNamedMatchIn(this CodeMatcher matcher, string name, out CodeInstruction instruction) {
            instruction = matcher.NamedMatch(name);
            return matcher;
        }

        /// <summary>
        /// Extracts the current <see cref="Instruction"/> out of a <see cref="CodeMatcher"/>.
        /// </summary>
        public static CodeMatcher RememberInstructionIn(this CodeMatcher matcher, out CodeInstruction instruction) {
            instruction = matcher.Instruction;
            return matcher;
        }


    }

    public static class Instruction {
        public static bool IsBrFalse(this CodeInstruction ci)
            => ci.opcode == OpCodes.Brfalse || ci.opcode == OpCodes.Brfalse_S;

        public static bool IsBrTrue(this CodeInstruction ci)
            => ci.opcode == OpCodes.Brtrue || ci.opcode == OpCodes.Brtrue_S;

        public static bool IsBr(this CodeInstruction ci)
            => ci.opcode == OpCodes.Br || ci.opcode == OpCodes.Br_S;

        public static bool IsCallVirt(this CodeInstruction ci, MethodBase target = null)
            => target != null
                ? ci.opcode == OpCodes.Callvirt && ci.operand as MethodBase == target
                : ci.opcode == OpCodes.Callvirt;

        public static bool IsLdLoc(this CodeInstruction ci, Func<LocalBuilder, bool> predicate = null)
            => (ci.opcode == OpCodes.Ldloc || ci.opcode == OpCodes.Ldloc_S)
                && ci.operand is LocalBuilder lb
                && (predicate?.Invoke(lb) ?? true);

        public static bool IsLdLoc(this CodeInstruction ci, Type localType)
            => ci.IsLdLoc(lb => lb.LocalType == localType);

        public static bool IsInst(this CodeInstruction ci, Type type)
            => ci.opcode == OpCodes.Isinst && ci.operand as Type == type;
    }
}
