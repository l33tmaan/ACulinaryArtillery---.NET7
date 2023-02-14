using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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

        public static bool IsLdLoc(this CodeInstruction ci)
            => ci.opcode == OpCodes.Ldloc_0
                || ci.opcode == OpCodes.Ldloc_1
                || ci.opcode == OpCodes.Ldloc_2
                || ci.opcode == OpCodes.Ldloc_3
                || IsLdLoc(ci);

        public static bool IsStLoc(this CodeInstruction ci, Func<LocalBuilder, bool> predicate = null)
            => (ci.opcode == OpCodes.Stloc || ci.opcode == OpCodes.Stloc_S)
                && ci.operand is LocalBuilder lb
                && (predicate?.Invoke(lb) ?? true);

        public static bool IsStLoc(this CodeInstruction ci, Type localType)
            => ci.IsStLoc(lb => lb.LocalType == localType);

        public static bool IsAnyStLoc(this CodeInstruction ci)
            => ci.opcode == OpCodes.Stloc_0
                || ci.opcode == OpCodes.Stloc_1
                || ci.opcode == OpCodes.Stloc_2
                || ci.opcode == OpCodes.Stloc_3
                || IsStLoc(ci);

        public static bool IsInst(this CodeInstruction ci, Type type)
            => ci.opcode == OpCodes.Isinst && ci.operand as Type == type;
    }

    /// <summary>
    /// Helper class to allow easy(ish) replacements of local uses by type.
    /// </summary>
    public class LocalRedirector {

        private LocalRedirector() {
        }

        /// <summary>
        /// The singleton to use for access.
        /// </summary>
        public static LocalRedirector Instance { get; } = new LocalRedirector();

        /// <summary>
        /// Factories to replace all <c>[St|Ld]loc[_(0|1|2|3)|S] [operand]</c> instructions with <c>[St|Ld]loc [targetLocalBuilder]</c>
        /// </summary>
        private static IDictionary<OpCode, (OpCode Store, OpCode Load, System.Func<LocalBuilder, object, (Predicate<CodeInstruction> Matcher, Action<CodeInstruction> Mutator)> Factory, bool CaptureOperand)> localRewriterFactories =
           new (OpCode Store, OpCode Load, bool CaptureLocal)[] {
                // all relevant store/load tuples
                (OpCodes.Stloc, OpCodes.Ldloc, true),
                (OpCodes.Stloc_S, OpCodes.Ldloc_S, true),
                (OpCodes.Stloc_0, OpCodes.Ldloc_0, false),
                (OpCodes.Stloc_1, OpCodes.Ldloc_1, false),
                (OpCodes.Stloc_2, OpCodes.Ldloc_2, false),
                (OpCodes.Stloc_3, OpCodes.Ldloc_3, false),
           }
           .ToDictionary(
               t => t,
               // create matchers & mutators for each tuple
               t => (System.Func<LocalBuilder, object, (Predicate<CodeInstruction> Matcher, Action<CodeInstruction> Mutator)>)(
                    (LocalBuilder lb, object operand) => {
                        Predicate<CodeInstruction> simpleMatcher = ci => ci.opcode == t.Store || ci.opcode == t.Load;
                        return (
                            Matcher: t.CaptureLocal
                                ? ci => simpleMatcher(ci) && ci.operand == operand
                                : simpleMatcher,
                            Mutator: ci => {
                                ci.operand = lb;
                                ci.opcode = ci.opcode == t.Store
                                    ? OpCodes.Stloc
                                    : OpCodes.Ldloc;
                            }
                        );
                    })
            ).SelectMany(
               // flatten list by duplicating via both store & load opcodes per tuple
               kvp => new[] { 
                   (kvp.Key.Load, kvp.Key, kvp.Value, kvp.Key.CaptureLocal), 
                   (kvp.Key.Store, kvp.Key, kvp.Value, kvp.Key.CaptureLocal) 
               }
            ).ToDictionary(
               // make a new dictionary indexed by each opcode
               t => t.Item1,
               t => (Store: t.Key.Store, Load: t.Key.Load, Factory: t.Value, CaptureOperand: t.CaptureLocal)
            );

        /// <summary>
        /// Returns a nullable tuple of helpers to find &amp; change usages of the locals referenced by <paramref name="instruction"/>
        /// with elements:
        /// <list type="table">
        /// <item>
        /// <term>Matcher</term>
        /// <description>A <see cref="Predicate{CodeInstruction}" /> that will match any use of the same local variable as referenced by 
        /// <see cref="Instruction"/>'s <see cref="CodeInstruction.opcode" />, either for storage or retrieval.</description>
        /// </item>
        /// <item>
        /// <term>Mutator</term>
        /// <description><para>An <see cref="Action{CodeInstruction}"/> that will change an instructon to use <paramref name="targetLocalBuilder"/> 
        /// instead of the local from <see cref="Instruction"/>'s <see cref="CodeInstruction.opcode"/>.
        /// </para>
        /// <para>
        /// Note that this action will <em>blindly</em> replace the members of passed in instructions. If that is not intended, filter first (for 
        /// example via the Matcher).
        /// </para>
        /// </description>
        /// </item>
        /// <item>
        /// <term>CapturesOperand</term>
        /// <description>A flag that indicates if the <paramref name="instruction"/>'s <see cref="CodeInstruction.operand"/> is captured by 
        /// the Matcher and Mutator members (shortform <see cref="OpCode"/>s like <see cref="OpCodes.Ldarg_0"/>, <see cref="OpCodes.Stloc_1" />, etc.)
        /// don't use an operand. Longform variants like <see cref="OpCodes.Ldloc" /> or <see cref="OpCodes.Stloc_S" /> do.
        /// </description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="instruction">Instruction to create helpers for.</param>
        /// <param name="targetLocalBuilder">Local builder to redirect local usages to.</param>
        /// <remarks>Result will be <see langword="null" /> if <paramref name="instruction"/> is not a <see cref="OpCodes.Ldloc"/>/<see cref="OpCodes.Stloc"/>
        /// like instruction.</remarks>
        public (Predicate<CodeInstruction> Matcher, Action<CodeInstruction> Mutator, bool CapturesOperand)? this[CodeInstruction instruction, LocalBuilder targetLocalBuilder] {
            get {
                if (localRewriterFactories.TryGetValue(instruction.opcode, out var entry)) {
                    var (matcher, mutator) = entry.Factory(targetLocalBuilder, entry.CaptureOperand ? instruction.operand : null);
                    return (matcher, mutator, entry.CaptureOperand);
                } 
                return null;
                
            }
        }
    }

}
