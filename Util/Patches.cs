using ACulinaryArtillery.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

using System.Text;
using System.Threading.Tasks;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
//using CodeInstruction = System.Reflection.CodeIn

namespace ACulinaryArtillery
{
    [HarmonyPatch(typeof(InventorySmelting))]
    class SmeltingInvPatches
    {
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("GetOutputText")]
        /// static void displayFix(ref string __result, InventorySmelting __instance)
        /// {
        ///     if (__instance[1].Itemstack?.Collectible is BlockSaucepan)
        ///     {
        ///         __result = (__instance[1].Itemstack.Collectible as BlockSaucepan).GetOutputText(__instance.Api.World, __instance);
        ///     }
        /// }


        /// <summary>
        /// Turns the
        /// <code>
        ///     ...
		///	    if (targetSlot == this.slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockCookingContainer))
		///	    {
        ///	        ...
		///	    }  
        ///	    ...
        /// </code>
        /// block
        /// into
        /// <code>
        ///     ...
		///	    if (targetSlot == this.slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockSaucePan || stack.Collectible is BlockCookingContainer))
		///	    {
        ///	        ...
		///	    }  
        ///	    ...
        /// </code>
        /// to make saucepans/cauldrons prefer a firepit's input slot.
        /// </summary>
        /// 

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySmelting), nameof(InventorySmelting.GetSuitability))]
        public static bool Harmony_InventorySmelting_GetSuitability_Prefix(
            ItemSlot sourceSlot, ItemSlot targetSlot, ItemSlot[] ___slots, ref float __result)
        {
            var stack = sourceSlot.Itemstack;
            if (targetSlot == ___slots[1] && stack.Collectible is BlockSaucepan)
            {
                __result = 2.2f;
                return false;
            }
            return true;
        }
        // Thanks Apache!!!

        /* [HarmonyTranspiler]
        [HarmonyPatch(nameof(InventorySmelting.GetSuitability))]
        public static IEnumerable<CodeInstruction> AddSaucePanToPreferredSmeltingInputs(IEnumerable<CodeInstruction> instructions) {
            CodeMatcher matcher = new CodeMatcher(instructions);

            try {
                matcher
                    .MatchEndForward(
                        Code.Ldloc_0,
                        Code.Callvirt,
                        new CodeMatch(ci => Instruction.IsInst(ci, typeof(BlockSmeltingContainer))),
                        new CodeMatch(Instruction.IsBrTrue)
                    )

                    .ThrowIfInvalid("Transpiler anchor not found")

                    .RememberPositionIn(out var idxCheckEnd);

                matcher
                    .Advance(1)
                    .Insert(
                        matcher
                            .InstructionsInRange(idxCheckEnd - 3, idxCheckEnd)
                            .Manipulator(ci => ci.IsInst(typeof(BlockSmeltingContainer)), ci => ci.operand = typeof(BlockSaucepan))
                        );


                }
            catch (InvalidOperationException ex) {
                ACulinaryArtillery.LogError(ex.Message);
                return instructions;
            }

            return matcher.InstructionEnumeration();
        } */
    }


    class SqueezeHoneyAndCrackEggPatches {

        static Type[] ParameterSet_BlockPos = new[] { typeof(BlockPos) };
        static Type[] ParameterSet_BlockPosItemStackFloat = new[] { typeof(BlockPos), typeof(ItemStack), typeof(float) };
        static Type[] ParameterSet_ItemStack = new[] { typeof(ItemStack) };
        static Type[] ParameterSet_ItemStackItemStackFloat = new[] { typeof(ItemStack), typeof(ItemStack), typeof(float) };

        static MethodInfo BlockLiquidContainerBase_IsFull_ByPos = AccessTools.Method(typeof(BlockLiquidContainerBase), nameof(BlockLiquidContainerBase.IsFull), ParameterSet_BlockPos);
        static MethodInfo BlockLiquidContainerBase_IsFull_ByStack = AccessTools.Method(typeof(BlockLiquidContainerBase), nameof(BlockLiquidContainerBase.IsFull), ParameterSet_ItemStack);
        static MethodInfo BlockLiquidContainerBase_TryPutLiquid_ByPos = AccessTools.Method(typeof(BlockLiquidContainerBase), nameof(BlockLiquidContainerBase.TryPutLiquid), ParameterSet_BlockPosItemStackFloat);
        static MethodInfo BlockLiquidContainerBase_TryPutLiquid_ByStack = AccessTools.Method(typeof(BlockLiquidContainerBase), nameof(BlockLiquidContainerBase.TryPutLiquid), ParameterSet_ItemStackItemStackFloat);

        static MethodInfo ILiquidInterface_IsFull_ByPos = AccessTools.Method(typeof(ILiquidInterface), nameof(ILiquidInterface.IsFull), ParameterSet_BlockPos);
        static MethodInfo ILiquidInterface_IsFull_ByStack = AccessTools.Method(typeof(ILiquidInterface), nameof(ILiquidInterface.IsFull), ParameterSet_ItemStack);
        static MethodInfo ILiquidSink_TryPutLiquid_ByPos = AccessTools.Method(typeof(ILiquidSink), nameof(ILiquidSink.TryPutLiquid), ParameterSet_BlockPosItemStackFloat);
        static MethodInfo ILiquidSink_TryPutLiquid_ByStack = AccessTools.Method(typeof(ILiquidSink), nameof(ILiquidSink.TryPutLiquid), ParameterSet_ItemStackItemStackFloat);

        /// <summary>
        /// Harmony transpiler. Changes the execution of <see cref="ItemHoneyComb.CanSqueezeInto"/> &amp; <see cref="ItemHoneyComb.OnHeldInteractStop"/> in multiple ways:
        /// <list type="bullet">
        /// <item>
        /// Any
        /// <code><![CDATA[as BlockLiquidContainerTopOpened]]></code>
        /// or
        /// <code><![CDATA[is BlockLiquidContainerTopOpened]]></code>
        /// get replaced with 
        /// <code><![CDATA[as ILiquidSink]]></code>
        /// or
        /// <code><![CDATA[is ILiquidSink]]></code>
        /// respectively
        /// </item>
        /// <item>
        /// <para>
        /// Any calls to
        /// <code><![CDATA[BlockLiquidContainerBase.IsFull]]></code>
        /// or
        /// <code><![CDATA[BlockLiquidContainerBase.TryPutLiquid]]></code>
        /// get replaced with calls to
        /// <code><![CDATA[ILiquidInterface.IsFull]]></code>
        /// or
        /// <code><![CDATA[ILiquidSink.TryPutLiquid]]></code>
        /// respectively. 
        /// </para>
        /// <para>
        /// Applies to both overloads for each method.
        /// </para>
        /// </item>
        /// <item>
        /// Any assignments of <see cref="BlockLiquidContainerBase"/> local variables like
        /// <code><![CDATA[BlockLiquidContainerTopOpened blcto = ...]]></code>
        /// or uses of those variables get redirected to a new local variable
        /// <code><![CDATA[ILiquidSink ls = ...]]></code> and use that when referenced.
        /// </item>
        /// </list>
        /// </summary>
        [HarmonyPatch(typeof(ItemHoneyComb))]
        public class HoneyCombPatches_Part1 {

            [HarmonyTargetMethods]
            public static IEnumerable<MethodInfo> TargetMethods() {
                return new[] {
                    AccessTools.Method(typeof(ItemHoneyComb), nameof(ItemHoneyComb.CanSqueezeInto)),
                    AccessTools.Method(typeof(ItemHoneyComb), nameof(ItemHoneyComb.OnHeldInteractStop))
                };
            }

            /// <inheritdoc cref="HoneyCombPatches_Part1" />  
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> AllowSqueezeIntoAnyLiquidSink(
                IEnumerable<CodeInstruction> instructions, 
                ILGenerator ilGen
#if DEBUG
                , MethodBase target
#endif
                ) {
#if DEBUG
                var before = instructions.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));
#endif

                // method replacement map
                IDictionary<MethodInfo, MethodInfo> methodCallRedirects = new Dictionary<MethodInfo, MethodInfo> {
                    { BlockLiquidContainerBase_IsFull_ByPos, ILiquidInterface_IsFull_ByPos },
                    { BlockLiquidContainerBase_IsFull_ByStack, ILiquidInterface_IsFull_ByStack },
                    { BlockLiquidContainerBase_TryPutLiquid_ByPos, ILiquidSink_TryPutLiquid_ByPos },
                    { BlockLiquidContainerBase_TryPutLiquid_ByStack, ILiquidSink_TryPutLiquid_ByStack }
                };

                CodeMatcher matcher = new CodeMatcher(instructions, ilGen);
                try {
                    // if there is no use of a <see cref="BlockLiquidContainerTopOpened" /> local we dont need a new local, so create redirection local lazyly
                    Lazy<LocalBuilder> optionalReplacementLocal = new Lazy<LocalBuilder>(() => ilGen.DeclareLocal(typeof(ILiquidSink)));

                    // keep a dictionary of how many different locals use a <see cref="BlockLiquidContainerTopOpened" /> and how to find & replace them
                    IDictionary<(OpCode, object operand), (Predicate<CodeInstruction> predicate, Action<CodeInstruction> mutator)> localReplacements
                        = new Dictionary<(OpCode, object operand), (Predicate<CodeInstruction> predicate, Action<CodeInstruction> mutator)>();

                    // find all local variable uses of the replaced type casts, store lookup predicates & mutators to redirect them to our new local
                    while (matcher.Remaining > 0) {
                        matcher.MatchEndForward(
                                new CodeMatch(ci => Instruction.IsInst(ci, typeof(BlockLiquidContainerTopOpened))),
                                new CodeMatch(Instruction.IsAnyStLoc)
                            );

                        if (matcher.IsValid) {
                            var (localStoreMatcher, localStoreMutator, capturesOperand) = LocalRedirector.Instance[matcher.Instruction, optionalReplacementLocal.Value].Value;

                            var operand = capturesOperand
                                ? matcher.Operand
                                : null;

                            localReplacements[(matcher.Opcode, operand)] = (localStoreMatcher, localStoreMutator);
                        }
                    }

                    // we want to replace
                    // - all <c>as BlockLiquidContainerTopOpened</c> checks with <c>as ILiquidSink</c>
                    // - all <c>BlockLiquidContainerBase.IsFull/TryPutLiquid</c> calls with <c>ILiquidInterface.IsFull/ILiquidSink.TryPutLiquid</c> calls
                    System.Func<CodeInstruction, bool> predicate = ci => ci switch {
                        var c when c.opcode == OpCodes.Isinst => c.operand as Type == typeof(BlockLiquidContainerTopOpened),
                        var c when c.opcode == OpCodes.Callvirt => methodCallRedirects.ContainsKey(c.operand as MethodInfo),
                        _ => false
                    };
                    Action<CodeInstruction> mutator = ci => {
                        if (ci.opcode == OpCodes.Isinst) {
                            ci.operand = typeof(ILiquidSink);
                        } else if (ci.opcode == OpCodes.Callvirt && methodCallRedirects.TryGetValue(ci.operand as MethodInfo, out var redirect)) {
                            ci.operand = redirect;
                        }
                    };

                    // **additionally** we want to replace all uses (stores & retrievals) of any local of <c>BlockLiquidContainerTopOpened</c> with
                    // our new local of <c>ILiquidSink</c>
                    // NOTE: this may need additional base replacements if more than just <c>IsFull/TryPutLiquid</c> is called on these local(s)
                    (predicate, mutator) = localReplacements
                        .Aggregate(
                            (predicate, mutator),
                            (aggregate, entry) => {
                                var (currentPredicate, currentMutator) = aggregate;
                                return (
                                    ci => currentPredicate(ci) || entry.Value.predicate(ci),
                                    ci => {
                                        if (entry.Value.predicate(ci))
                                            entry.Value.mutator(ci);
                                        else
                                            currentMutator(ci);
                                    }                                
                                );
                            }
                        );

                    var result = instructions.Manipulator(
                        predicate,
                        mutator)
                        .ToList();

#if DEBUG
                    var after = result.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));

                    System.Diagnostics.Debug.WriteLine($"--- {target.DeclaringType}.{target.Name}, Patch {nameof(HoneyCombPatches_Part1)}.{nameof(AllowSqueezeIntoAnyLiquidSink)} ---");
                    System.Diagnostics.Debug.Write(before);
                    System.Diagnostics.Debug.WriteLine("=>");
                    System.Diagnostics.Debug.Write(after);
                    System.Diagnostics.Debug.WriteLine("---------------------------------");
#endif
                    return result;

                } catch (InvalidOperationException ex) {
                    ACulinaryArtillery.LogError(ex.Message);
                    return instructions;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Patches <see cref="ItemHoneyComb.CanSqueezeInto"/> to not allow squeezing into a sealed <see cref="BlockEntitySaucepan"/> 
        /// </para>
        /// <para>
        /// Changes the excution flow from
        /// <code><![CDATA[
        /// if (... != null) {
		///     ...
		/// }
        /// ]]>
        /// </code>
        /// into
        /// <code><![CDATA[
        /// if (... != null && !HoneyCombPatches_Part2.IsSealedSaucePan(this.api, pos)) {
		///     ...
		/// }
        /// ]]>
        /// </code>
        /// </para>
        /// </summary>
        /// <remarks>Functionality to allow squeezing into <see cref="BlockEntitySaucepan"/>s is actually added by another transpiler. 
        /// So yes, this is not a nonsensical patch just because it prevents something which vanilla would never do anyway ;)
        /// See <see cref="HoneyCombPatches_Part1"/>.</remarks>
        [HarmonyPatch(typeof(ItemHoneyComb), nameof(ItemHoneyComb.CanSqueezeInto))]
        public class HoneyCombPatches_Part2 {

            /// <inheritdoc cref="HoneyCombPatches_Part2" />  
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> BlockSqueezingOnSealedSaucepans(
                IEnumerable<CodeInstruction> instructions, 
                ILGenerator ilGen
#if DEBUG
                , MethodBase target
#endif
                ) {
#if DEBUG
                var before = instructions.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));
#endif

                FieldInfo fieldApi = AccessTools.Field(typeof(CollectibleObject), "api");
                MethodInfo getInventoryAccessor = AccessTools.PropertyGetter(typeof(BlockEntityContainer), nameof(BlockEntityContainer.Inventory));

                var matcher = new CodeMatcher(instructions);
                try {
                   
                    // find first <c>... != null</c> instruction
                    matcher.MatchEndForward(
                            new CodeMatch(Instruction.IsBrFalse)
                        )

                        .ThrowIfInvalid("Could not find transpiler anchor.");

                    // remember jump target
                    var label = matcher.Operand;

                    // add <c>&& !IsSealedSaucePan(this.api, pos)</c> into if check
                    matcher.Advance(1);
                    matcher.Insert(
                        new CodeInstruction(OpCodes.Ldarg_0),                                                                                                       // <c>this</c>
                        new CodeInstruction(OpCodes.Ldfld, fieldApi),                                                                                               // <c>.api</c>
                        new CodeInstruction(OpCodes.Ldarg_2),                                                                                                       // <c>, pos</c>
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HoneyCombPatches_Part2), nameof(HoneyCombPatches_Part2.IsSealedSaucePan))),   // <c>IsSealedSaucePan(...)
                        new CodeInstruction(OpCodes.Brtrue, label));                                                                                               // <c>&& !...)

                    // dont need to touch 'BlockEntityGroundStorage beg' branch in the same manner since that recursively calls back into itself via the block branch.                    

                    var result = matcher.InstructionEnumeration().ToList();
#if DEBUG
                    var after = result.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));

                    System.Diagnostics.Debug.WriteLine($"--- {target.DeclaringType}.{target.Name}, Patch {nameof(HoneyCombPatches_Part2)}.{nameof(BlockSqueezingOnSealedSaucepans)} ---");
                    System.Diagnostics.Debug.Write(before);
                    System.Diagnostics.Debug.WriteLine("=>");
                    System.Diagnostics.Debug.Write(after);
                    System.Diagnostics.Debug.WriteLine("---------------------------------");
#endif

                    return result;                   

                } catch (InvalidOperationException ex) {
                    ACulinaryArtillery.LogError(ex.Message);
                    return instructions;
                }
            }

            public static bool IsSealedSaucePan(ICoreAPI api, BlockPos pos) {
                return pos != null && api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntitySaucepan bs && bs.isSealed;
            }
        }


        /// <summary>
        /// Harmony transpiler. Changes the execution of <see cref="ItemHoneyComb.OnHeldInteractStop(float, ItemSlot, EntityAgent, BlockSelection, EntitySelection)"/> 
        /// &amp; <see cref="ItemHoneyComb.OnHeldInteractStart(ItemSlot, EntityAgent, BlockSelection, EntitySelection, bool, ref EnumHandHandling)"/> by replacing any call of
        /// <code><![CDATA[
        /// ....CanSqueezeInto(..., blockSel.Position)
        /// ]]></code>
        /// with
        /// <code><![CDATA[
        /// HoneyCombPatches_Part3.CanSqueezeInto(..., ..., blockSel)
        /// ]]></code>
        /// The effect is that ground storage now uses that <c>blockSel</c> parameter to <em>choose</em> which container to squeeze into (if available). Makes the 
        /// bahaviour consistent with egg cracking.
        /// </summary>
        /// <remarks>
        /// Effect cannot be achieved by transpiling <see cref="ItemHoneyComb.CanSqueezeInto" /> since a required parameter is not even passed to
        /// that method.</remarks>        
        [HarmonyPatch(typeof(ItemHoneyComb))]
        public class HoneyCombPatches_Part3 {

            private static  AccessTools.FieldRef<ItemHoneyComb, ICoreAPI> apiAccessor = AccessTools.FieldRefAccess<ItemHoneyComb, ICoreAPI>("api");

            [HarmonyTargetMethods]
            public static IEnumerable<MethodInfo> TargetMethods() {
                return new[] {
                    AccessTools.Method(typeof(ItemHoneyComb), nameof(ItemHoneyComb.OnHeldInteractStart)),
                    AccessTools.Method(typeof(ItemHoneyComb), nameof(ItemHoneyComb.OnHeldInteractStop))
                };
            }

            /// <inheritdoc cref="HoneyCombPatches_Part3" />         
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> ReplaceCanSqueezeIntoCalls(
                IEnumerable<CodeInstruction> instructions
#if DEBUG
                , MethodBase target
#endif
                ) {
#if DEBUG
                var before = instructions.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));
#endif

                MethodInfo targetMethod = AccessTools.Method(typeof(ItemHoneyComb), nameof(ItemHoneyComb.CanSqueezeInto));
                MethodInfo redirectMethod = AccessTools.Method(typeof(HoneyCombPatches_Part3), nameof(HoneyCombPatches_Part3.CanSqueezeInto));
                FieldInfo positionField = AccessTools.Field(typeof(BlockSelection), nameof(BlockSelection.Position));

                CodeMatcher matcher = new CodeMatcher(instructions);
                try {
                    while (matcher.Remaining > 0) {
                        matcher.MatchStartForward(
                            new CodeMatch(OpCodes.Ldfld, positionField),
                            new CodeMatch(OpCodes.Call, targetMethod)
                        );

                        if (matcher.IsValid) {
                            matcher.SetOpcodeAndAdvance( OpCodes.Nop );     // remove the <c>.Position</c> call
                            matcher.SetOperandAndAdvance(redirectMethod);
                        }
                    }

                    var result = matcher.InstructionEnumeration().ToList();
#if DEBUG
                    var after = result.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));

                    System.Diagnostics.Debug.WriteLine($"--- {target.DeclaringType}.{target.Name}, Patch {nameof(HoneyCombPatches_Part3)}.{nameof(ReplaceCanSqueezeIntoCalls)} ---");
                    System.Diagnostics.Debug.Write(before);
                    System.Diagnostics.Debug.WriteLine("=> => =>");
                    System.Diagnostics.Debug.Write(after);
                    System.Diagnostics.Debug.WriteLine("---------------------------------");
#endif

                    return result;
                } catch (InvalidOperationException ex) {
                    ACulinaryArtillery.LogError(ex.Message);
                    return instructions;
                }

            }

            /// <summary>
            /// This is <em>kind of</em> a prefix for <see cref="ItemHoneyComb.CanSqueezeInto(Block, BlockPos)"/>, but it uses a different 
            /// set of parameters. Still partially shadows calls into there.
            /// </summary>
            public static bool CanSqueezeInto(ItemHoneyComb instance, Block block, BlockSelection selection) {
                return block switch {
                    ILiquidSink => instance.CanSqueezeInto(block, selection.Position),
                    _ => apiAccessor(instance) is var api
                        && api.World.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityGroundStorage beg
                        && instance.GetSuitableTargetSlot(beg, selection) is ItemSlot
                };
            }

        }

        /// <summary>
        /// Harmony transpiler. Changes the execution flow of <see cref="ItemHoneyComb.OnHeldInteractStop"/> so that
        /// <code><![CDATA[
        /// ...
        /// ItemSlot squeezeIntoSlot = beg.Inventory.FirstOrDefault(delegate (ItemSlot gslot)
        /// {
        ///     ItemStack itemstack = gslot.Itemstack;
        ///     return ((itemstack != null) ? itemstack.Block : null) != null && this.CanSqueezeInto(gslot.Itemstack.Block, null);
        /// });
        /// ...
        /// ]]>
        /// </code>
        /// gets replaced with
        /// <code><![CDATA[
        /// ...
        /// ItemSlot squeezeIntoSlot = SqueezeHelper.GetSuitableTargetSlot(this, beg, blockSel);
        /// ...
        /// ]]>
        /// </code>
        /// </summary>
        [HarmonyPatch(typeof(ItemHoneyComb), nameof(ItemHoneyComb.OnHeldInteractStop))]
        public class HoneyCombPatches_Part4 {

            /// <inheritdoc cref="HoneyCombPatches_Part4" />         
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> ReplaceGroundStorageSqueezeTargetSelection(
                IEnumerable<CodeInstruction> instructions
#if DEBUG
                , MethodBase target
#endif
                ) {
#if DEBUG
                var before = instructions.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));
#endif

                MethodInfo firstOrDefault = typeof(Enumerable)
                    .GetMethods()
                    .Single(
                        mi => mi is { Name: nameof(Enumerable.FirstOrDefault), IsGenericMethod: true }                      // `Enumerable.FirstOrDefault<...>(...)` method
                            && mi.GetParameters() is [_, { ParameterType: var pt }]                                         // with exactly **two** parameters
                            && mi.GetGenericArguments() is [var ga]                                                         // with **one** generic type argument `<T>`
                            && pt == typeof(System.Func<,>).MakeGenericType(ga, typeof(bool))                               // where the second parameter is of Type `Func<T, bool>`
                    );
                MethodInfo firstOrDefaultOfItemSlot = firstOrDefault.MakeGenericMethod(typeof(ItemSlot));
                MethodInfo entityContainerInventoryGetter = AccessTools.PropertyGetter(typeof(BlockEntityContainer), nameof(BlockEntityContainer.Inventory));


                CodeMatcher matcher = new CodeMatcher(instructions);
                try {
                    matcher
                        .End()
                        // find the <c>beg.Inventory.FirstOrDefault(delegate ...) { ... }</c> block
                        .MatchStartBackwards(
                            new CodeMatch(ci => Instruction.IsLdLoc(ci, typeof(BlockEntityGroundStorage))),
                            new CodeMatch(ci => Instruction.IsCallVirt(ci, entityContainerInventoryGetter)),
                            Code.Ldarg_0,
                            Code.Ldftn,
                            Code.Newobj,
                            new CodeMatch(OpCodes.Call, firstOrDefaultOfItemSlot)
                        )
                        .ThrowIfInvalid("Could not find transpiler anchor")

                        .Advance(1)                                                                                         // step past the 'beg' local getter
                        .RemoveInstructions(5)                                                                              // remove <c>.Inventory.FirstOrDefault(delegate ...) { ... }</c> part
                        .Advance(-1)                                                                                        // step in front of the 'beg' local getter again
                        .Insert(new CodeInstruction(OpCodes.Ldarg_0))                                                       // add <c>this</c>
                        .Advance(2)                                                                                         // step past 'beg' again ;)
                        .Insert(
                            new CodeInstruction(OpCodes.Ldarg_S, 4),                                                        // add <c>blockSel</c>
                            new CodeInstruction(
                                OpCodes.Call, 
                                AccessTools.Method(typeof(SqueezeHelper), nameof(SqueezeHelper.GetSuitableTargetSlot)))     // call <c>SqueezeHelper.GetSuitableTargetSlot</c>
                        );

                    var result = matcher.InstructionEnumeration().ToList();
#if DEBUG
                    var after = result.Aggregate(new StringBuilder(), (sb, ci) => sb.AppendLine(ci.ToString()));

                    System.Diagnostics.Debug.WriteLine($"--- {target.DeclaringType}.{target.Name}, Patch {nameof(HoneyCombPatches_Part4)}.{nameof(ReplaceGroundStorageSqueezeTargetSelection)} ---");
                    System.Diagnostics.Debug.Write(before);
                    System.Diagnostics.Debug.WriteLine("=> => =>");
                    System.Diagnostics.Debug.Write(after);
                    System.Diagnostics.Debug.WriteLine("---------------------------------");
#endif

                    return result;
                } catch (InvalidOperationException ex) {
                    ACulinaryArtillery.LogError(ex.Message);
                    return instructions;
                }

            }
        }
    }

    [HarmonyPatch(typeof(CookingRecipeIngredient))]
    class CookingIngredientPatches
    {
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("GetMatchingStack")]
        static bool displayFix(ItemStack inputStack, ref CookingRecipeStack __result, CookingRecipeIngredient __instance)
        {
            if (inputStack == null)
            { __result = null; return false; }

            for (int i = 0; i < __instance.ValidStacks.Length; i++)
            {
                bool isWildCard = __instance.ValidStacks[i].Code.Path.Contains("*");
                bool found =
                    (isWildCard && inputStack.Collectible.WildCardMatch(__instance.ValidStacks[i].Code))
                    || (!isWildCard && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats" }).ToArray()))
                    || (__instance.ValidStacks[i].CookedStack?.ResolvedItemstack != null && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats" }).ToArray()))
                ;

                if (found)
                { __result = __instance.ValidStacks[i]; return false; }
            }


            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityShelf))]
    class ShelfPatches
    {
        static MethodBase miBlockEntityShelf_GetBlockInfo = AccessTools.Method(typeof(BlockEntityShelf), nameof(BlockEntityShelf.GetBlockInfo));
        static MethodInfo miBlockEntityShelf_CrockInfoCompact = AccessTools.Method(typeof(BlockEntityShelf), nameof(BlockEntityShelf.CrockInfoCompact));
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}


        [HarmonyTranspiler]
        [HarmonyPatch(nameof(BlockEntityShelf.GetBlockInfo))]
        /// <summary>
        /// Modifes parts of the <see cref="BlockEntityShelf.GetBlockInfo" /> method:
        /// Turns 
        /// <code>
        ///     ...
        ///     if (stack.Collectible is BlockCrock) {
        ///         sb.Append(this.CrockInfoCompact(this.inv[j]));
        ///     } else if (...) {
        ///         ...
        ///     } 
        ///     ...
        /// </code>
        /// into
        /// <code>
        ///     ...
        ///     if (stack.Collectible is BlockCrock) {
        ///         sb.Append(this.CrockInfoCompact(this.inv[j]));
        ///     } else if (stack.Collectible is BlockLiquidContainerBase) {
        ///         sb.Append(LiquidInfoCompact(this, this.inv[j]));
        ///     } else if (...) {
        ///         ...
        ///     } 
        ///     ...
        /// </code>
        /// </summary>
        /// <remarks>
        /// Don't use Prefixes. Prefixes are evil. Prefixes don't play with others.
        /// </remarks>
        public static IEnumerable<CodeInstruction> AddLiquidContainerInfo(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen) {
            // methods & types used for finding code instructions (or building them)
            MethodInfo miItemStackGetCollectible = AccessTools.PropertyGetter(typeof(ItemStack), nameof(ItemStack.Collectible));
            MethodInfo miCrockInfoCompact = AccessTools.Method(typeof(BlockEntityShelf), nameof(BlockEntityShelf.CrockInfoCompact));

            Type typeBlockCrock = typeof(BlockCrock);

            const string jumpNoCrockBranch = "branchNotACrock";

            var matcher = new CodeMatcher(instructions, ilGen);

            // find the start of the <c>if (stack.Collectible is BlockCrock) { ... }</c> block
            try {
                matcher
                    .MatchStartForward(
                        new CodeMatch(ci => Instruction.IsLdLoc(ci, typeof(ItemStack))),                                                            // <c>???</c>  - _technically_ this matches on using _any_ ItemStack typed local variable
                        new CodeMatch(ci => Instruction.IsCallVirt(ci, miItemStackGetCollectible)),                                                 // <c>.Collectible</c>
                        new CodeMatch(ci => Instruction.IsInst(ci, typeBlockCrock)),                                                                // <c>is BlockCrock</c>                                                     
                        new CodeMatch(Instruction.IsBrFalse, jumpNoCrockBranch)                                                                     // <c>) {... </c>                                                                               
                    )
                    .ThrowIfInvalid("Cannot find transpiler anchor")

                    .RememberPositionIn(out var idxStart)                                                                                           // remember current instruction position
                    .RememberNamedMatchIn(jumpNoCrockBranch, out var ciBranchNoCrock)                                                               // remember marked instruction

                    .MatchEndForward(
                        new CodeMatch(Instruction.IsBr)                                                                                             // <c>... }</c>
                    )
                    .ThrowIfInvalid("Cannot find branch block end")

                    .RememberPositionIn(out var idxEnd)                                                                                             // remember current instruction position                                                                                                                                    

                    .Advance(1)                                                                                                                     // insert our new "if" block *after* the "if" block for the crock
                    .Insert(                        
                        matcher
                            .InstructionsInRange(idxStart, idxEnd)                                                                                  // create a copy of the <c>if (stack.Collectible is BlockCrock) { ... }</c> block but
                            .MethodReplacer(miCrockInfoCompact, AccessTools.Method(typeof(ShelfPatches), nameof(ShelfPatches.LiquidInfoCompact)))   //   - replace <c>CrockInfoCompact(...)</c> call with <c>LiquidInfoCompact(...)</c> call
                            .Manipulator(ci => ci.IsInst(typeBlockCrock), ci => ci.operand = typeof(BlockLiquidContainerBase))                      //   - replace <c>is BlockCrock</c> with <c>is BlockLiquidContainerBase</c>
                    )
                    .CreateLabel(out Label newBranchLabel);
                
                ciBranchNoCrock.operand = newBranchLabel;                                                                                           // make <c>(... is BlockCrock)</c> jump into our new branch on failure

            } catch (InvalidOperationException ex) {
                ACulinaryArtillery.LogError(ex.Message);
                return instructions;
            }

            return matcher.InstructionEnumeration();
        }

        public static string LiquidInfoCompact(BlockEntityShelf f, ItemSlot slot) {
            var sb = new StringBuilder();
            (slot.Itemstack.Collectible as BlockLiquidContainerBase).GetContentInfo(slot, sb, f.Api.World);
            return $"{slot.Itemstack.GetName()} ({sb.Replace(Environment.NewLine, " ").ToString().TrimEnd(' ')}){Environment.NewLine}";
        }


    }



    /// [HarmonyPatch(typeof(BlockEntityDisplay))]
    /// class DisplayPatches
    /// {
    ///     //[HarmonyPrepare]
    ///     //static bool Prepare()
    ///     //{
    ///     //    return true;
    ///     //}

    ///     [HarmonyPrefix]
    ///     [HarmonyPatch("genMesh")]
    ///     static bool displayFix(ItemStack stack, BlockEntityDisplay __instance, ref MeshData __result)
    ///     //static bool displayFix(ItemStack stack, ref MeshData __result, BlockEntityDisplay __instance, ref Item ___nowTesselatingItem)
    ///     {
    ///         if (!(stack.Collectible is ItemExpandedRawFood))
    ///             return true;
    ///         string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
    ///         if (ings == null || ings.Length <= 0)
    ///             return true;

     ///        //___nowTesselatingItem = stack.Item;

     ///        __result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, stack, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
     ///        //__result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, __instance, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
     ///        if (__result != null)
     ///            __result.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
     ///        else
     ///            return true;


     ///        if (stack.Collectible.Attributes?[__instance.AttributeTransformCode].Exists == true)
     ///        {
     ///            ModelTransform transform = stack.Collectible.Attributes?[__instance.AttributeTransformCode].AsObject<ModelTransform>();
     ///            transform.EnsureDefaultValues();
     ///            transform.Rotation.Y += __instance.Block.Shape.rotateY;
     ///            __result.ModelTransform(transform);
     ///        }

     ///        //if (__instance.Block.Shape.rotateY == 90 || __instance.Block.Shape.rotateY == 270) __result.Rotate(new Vec3f(0f, 0f, 0f), 0f, 90 * GameMath.DEG2RAD, 0f);

     ///        return false;
     ///    }
     /// }



    [HarmonyPatch(typeof(BlockEntityCookedContainer))]
    class BECookedContainerPatches
    {
        //This is for the cooking pot entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("BlockEntityCookedContainer", MethodType.Constructor)]
        static void invFix(ref InventoryGeneric ___inventory)
        {
            ___inventory = new InventoryGeneric(6, null, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("FromRecipe", MethodType.Getter)]
        static void recipeFix(ref CookingRecipe __result, BlockEntityCookedContainer __instance)
        {
            __result ??= MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetBlockInfo")]
        static bool infoFix(IPlayer forPlayer, ref StringBuilder dsc, BlockEntityCookedContainer __instance)
        {
            ItemStack[] contentStacks = __instance.GetNonEmptyContentStacks();
            CookingRecipe recipe = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
            if (recipe == null)
                return true;

            float servings = __instance.QuantityServings;
            int temp = (int)contentStacks[0].Collectible.GetTemperature(__instance.Api.World, contentStacks[0]);
            ;
            string temppretty = Lang.Get("{0}°C", temp);
            if (temp < 20)
                temppretty = "Cold";

            BlockMeal mealblock = __instance.Api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            string nutriFacts = mealblock.GetContentNutritionFacts(__instance.Api.World, __instance.Inventory[0], contentStacks, forPlayer.Entity);


            if (servings == 1)
            {
                dsc.Append(Lang.Get("{0} serving of {1}\nTemperature: {2}{3}{4}", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }
            else
            {
                dsc.Append(Lang.Get("{0} servings of {1}\nTemperature: {2}{3}{4}", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }


            foreach (var slot in __instance.Inventory)
            {
                if (slot.Empty)
                    continue;

                TransitionableProperties[] propsm = slot.Itemstack.Collectible.GetTransitionableProperties(__instance.Api.World, slot.Itemstack, null);
                if (propsm != null && propsm.Length > 0)
                {
                    slot.Itemstack.Collectible.AppendPerishableInfoText(slot, dsc, __instance.Api.World);
                    break;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityMeal))]
    class BEMealContainerPatches
    {
        //This is for the meal bowl entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("BlockEntityMeal", MethodType.Constructor)]
        static void invFix(ref InventoryGeneric ___inventory)
        {
            ___inventory = new InventoryGeneric(6, null, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("FromRecipe", MethodType.Getter)]
        static void recipeFix(ref CookingRecipe __result, BlockEntityMeal __instance)
        {
            __result ??= MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
        }
    }

    [HarmonyPatch(typeof(BlockCookedContainerBase))]
    class BlockMealContainerBasePatches
    {
        //This is for the base food container
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            __result ??= MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetMealRecipe")]
        static void mealFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            __result ??= MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }
    }

    [HarmonyPatch(typeof(BlockMeal))]
    class BlockMealBowlBasePatches
    {
        //This is for the food bowl block
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            __result ??= MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        
        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionProperties", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
        static bool nutriFix(IWorldAccessor world, ItemSlot inSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref FoodNutritionProperties[] __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            List<FoodNutritionProperties> props = new List<FoodNutritionProperties>();

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            

            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null)
                    continue;
                props.AddRange( ItemExpandedRawFood.GetExpandedContentNutritionProperties(
                                                                                            world,
                                                                                            inSlot,
                                                                                            contentStacks[i],
                                                                                            forEntity,
                                                                                            mulWithStacksize,
                                                                                            nutritionMul,
                                                                                            healthMul
                                                                                            ) );
            }

            __result = props.ToArray();
            return false;
        }
        

        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionFacts", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
        static bool nutriFactsFix(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref string __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            FoodNutritionProperties[] props;

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;
            float satLossMul = 1;
            float healthLossMul = 1;

            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null)
                    continue;
                DummySlot slot = new DummySlot(contentStacks[i], inSlotorFirstSlot.Inventory);
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);

                props = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, inSlotorFirstSlot, contentStacks[i], forEntity, mulWithStacksize, nutritionMul, healthMul);
                for (int j = 0; j < props.Length; j++)
                {
                    FoodNutritionProperties prop = props[j];
                    if (prop == null)
                        continue;
                    float sat = 0;
                    totalSaturation.TryGetValue(prop.FoodCategory, out sat);
                    totalHealth += prop.Health * healthLossMul;
                    totalSaturation[prop.FoodCategory] = sat + prop.Satiety * satLossMul;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Nutrition Facts"));

            foreach (var val in totalSaturation)
            {
                sb.AppendLine("- " + Lang.Get("" + val.Key) + ": " + Math.Round(val.Value) + " sat.");
            }
            if (totalHealth != 0)
            {
                sb.AppendLine("- " + Lang.Get("Health: {0}{1} hp", totalHealth > 0 ? "+" : "", totalHealth));
            }

            __result = sb.ToString();
            return false;
        }
    }


    [HarmonyPatch(typeof(BlockCrock))]
    class BlockCrockContainerPatches
    {
        //This is for the cooking pot entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("GetPlacedBlockInfo")]
        static bool infoFix(IWorldAccessor world, BlockPos pos, IPlayer forPlayer, BlockCrock __instance, ref string __result)
        {
            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null)
                return true;

            BlockMeal mealblock = world.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            CookingRecipe recipe = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault((rec) => becrock.RecipeCode == rec.Code);
            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();

            if (stacks == null || stacks.Length == 0)
            {
                return true;
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(world, stacks, forPlayer.Entity, becrock.inventory);

                if (recipe != null)
                {
                    if (becrock.QuantityServings == 1)
                    {
                        dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("{0} servings of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                }

                string facts = mealblock.GetContentNutritionFacts(world, new DummySlot(__instance.OnPickBlock(world, pos)), null);

                if (facts != null)
                {
                    dsc.Append(facts);
                }

                slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);
            }
            else
            {
                return true;
            }

            if (becrock.Sealed)
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }


            __result = dsc.ToString();
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityPie))]
    class BlockEntityPiePatch
    {
        //This is for the pie entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("TryAddIngredientFrom")]
        static bool mulitPie(ref bool __result, BlockEntityPie __instance, ItemSlot slot, IPlayer byPlayer = null)
        {
            InventoryBase inv = __instance.Inventory;
            ICoreClientAPI capi = __instance.Api as ICoreClientAPI;

            var pieProps = slot.Itemstack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>(null, slot.Itemstack.Collectible.Code.Domain);
            if (pieProps == null)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "notpieable", Lang.Get("This item can not be added to pies"));
                __result = false;
                return false;
            }

            if (slot.StackSize < 2)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "notpieable", Lang.Get("Need at least 2 items each"));
                __result = false;
                return false;
            }

            var pieBlock = (inv[0].Itemstack.Block as BlockPie);
            if (pieBlock == null)
            { __result = false; return false; }

            ItemStack[] cStacks = pieBlock.GetContents(__instance.Api.World, inv[0].Itemstack);

            bool isFull = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;
            bool hasFilling = cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null;

            if (isFull)
            {
                if (pieProps.PartType == EnumPiePartType.Crust)
                {
                    if (cStacks[5] == null)
                    {
                        cStacks[5] = slot.TakeOut(2);
                        pieBlock.SetContents(inv[0].Itemstack, cStacks);
                    }
                    else
                    {
                        ItemStack stack = inv[0].Itemstack;
                        stack.Attributes.SetInt("topCrustType", (stack.Attributes.GetInt("topCrustType") + 1) % 3);
                    }
                    __result = true;
                    return false;
                }
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "piefullfilling", Lang.Get("Can't add more filling - already completely filled pie"));
                __result = false;
                return false;
            }

            if (pieProps.PartType != EnumPiePartType.Filling)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "pieneedsfilling", Lang.Get("Need to add a filling next"));
                __result = false;
                return false;
            }


            if (!hasFilling)
            {
                cStacks[1] = slot.TakeOut(2);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }

            var foodCats = cStacks.Select(stack => stack?.Collectible.NutritionProps?.FoodCategory ?? stack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory ?? EnumFoodCategory.Vegetable).ToArray();
            var stackprops = cStacks.Select(stack => stack?.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>(null, stack.Collectible.Code.Domain)).ToArray();

            ItemStack cstack = slot.Itemstack;
            EnumFoodCategory foodCat = slot.Itemstack?.Collectible.NutritionProps?.FoodCategory ?? slot.Itemstack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory ?? EnumFoodCategory.Vegetable;

            bool equal = true;
            bool foodCatEquals = true;

            for (int i = 1; equal && i < cStacks.Length - 1; i++)
            {
                if (cstack == null)
                    continue;

                equal &= cStacks[i] == null || cstack.Equals(__instance.Api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                foodCatEquals &= cStacks[i] == null || foodCats[i] == foodCat;

                cstack = cStacks[i];
                foodCat = foodCats[i];
            }

            int emptySlotIndex = 2 + (cStacks[2] != null ? 1 + (cStacks[3] != null ? 1 : 0) : 0);

            if (equal)
            {
                cStacks[emptySlotIndex] = slot.TakeOut(2);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }

            if (inv.Count < 0)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "piefullfilling", Lang.Get("Can't mix fillings from different food categories"));
                __result = false;
                return false;
            }
            else
            {
                if (!stackprops[1].AllowMixing)
                {
                    if (byPlayer != null && capi != null)
                        capi.TriggerIngameError(__instance, "piefullfilling", Lang.Get("You really want to mix these to ingredients?! That would taste horrible!"));
                    __result = false;
                    return false;
                }

                cStacks[emptySlotIndex] = slot.TakeOut(2);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }
        }
    }
}


