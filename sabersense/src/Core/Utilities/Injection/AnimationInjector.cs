// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Core.Utilities.Injection;

internal static class AnimationInjector
{
    internal static void MirrorAnimations(GameObject root)
    {
        if (root == null) return;

        Transform? leftSaber = null, rightSaber = null;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            var child = root.transform.GetChild(i);
            if (child.name.Equals("LeftSaber", System.StringComparison.OrdinalIgnoreCase))
                leftSaber = child;
            else if (child.name.Equals("RightSaber", System.StringComparison.OrdinalIgnoreCase))
                rightSaber = child;
        }

        if (leftSaber == null || rightSaber == null) return;

        MirrorAnimators(leftSaber, rightSaber);
        MirrorLegacyAnimations(leftSaber, rightSaber);
    }

    private static void MirrorAnimators(Transform leftSaber, Transform rightSaber)
    {
        var leftAnimators = leftSaber.GetComponentsInChildren<Animator>(true);
        var rightAnimators = rightSaber.GetComponentsInChildren<Animator>(true);

        bool leftHas = leftAnimators.Length > 0;
        bool rightHas = rightAnimators.Length > 0;

        if (!leftHas && !rightHas) return;
        if (leftHas && rightHas) return;

        var source = leftHas ? leftSaber : rightSaber;
        var target = leftHas ? rightSaber : leftSaber;
        var sourceAnimators = leftHas ? leftAnimators : rightAnimators;

        var targetLookup = new Dictionary<string, Transform>();
        InjectionHelpers.CollectTransformsFlat(target, targetLookup);

        foreach (var srcAnimator in sourceAnimators)
        {
            if (srcAnimator.runtimeAnimatorController == null) continue;

            if (!targetLookup.TryGetValue(srcAnimator.transform.name, out var targetTransform))
                continue;

            if (targetTransform.GetComponent<Animator>() != null) continue;

            var dst = targetTransform.gameObject.AddComponent<Animator>();
            dst.runtimeAnimatorController = srcAnimator.runtimeAnimatorController;
            dst.avatar = srcAnimator.avatar;
            dst.applyRootMotion = srcAnimator.applyRootMotion;
            dst.updateMode = srcAnimator.updateMode;
            dst.cullingMode = srcAnimator.cullingMode;
            dst.speed = srcAnimator.speed;
            dst.keepAnimatorStateOnDisable = srcAnimator.keepAnimatorStateOnDisable;

            ModLogger.ForSource("AnimMirror").Info(
                $"Mirrored Animator to '{targetTransform.name}' controller='{srcAnimator.runtimeAnimatorController.name}'");
        }
    }

    private static void MirrorLegacyAnimations(Transform leftSaber, Transform rightSaber)
    {
        var leftAnims = leftSaber.GetComponentsInChildren<Animation>(true);
        var rightAnims = rightSaber.GetComponentsInChildren<Animation>(true);

        bool leftHas = leftAnims.Length > 0;
        bool rightHas = rightAnims.Length > 0;

        if (!leftHas && !rightHas) return;
        if (leftHas && rightHas) return;

        var source = leftHas ? leftSaber : rightSaber;
        var target = leftHas ? rightSaber : leftSaber;
        var sourceAnims = leftHas ? leftAnims : rightAnims;

        var targetLookup = new Dictionary<string, Transform>();
        InjectionHelpers.CollectTransformsFlat(target, targetLookup);

        foreach (var srcAnim in sourceAnims)
        {
            if (!targetLookup.TryGetValue(srcAnim.transform.name, out var targetTransform))
                continue;

            if (targetTransform.GetComponent<Animation>() != null) continue;

            var dst = targetTransform.gameObject.AddComponent<Animation>();
            dst.playAutomatically = srcAnim.playAutomatically;
            dst.wrapMode = srcAnim.wrapMode;
            dst.animatePhysics = srcAnim.animatePhysics;
            dst.cullingType = srcAnim.cullingType;

            foreach (AnimationState state in srcAnim)
            {
                if (state.clip != null)
                    dst.AddClip(state.clip, state.name);
            }

            if (srcAnim.clip != null)
                dst.clip = srcAnim.clip;

            ModLogger.ForSource("AnimMirror").Info(
                $"Mirrored Animation to '{targetTransform.name}' clips={srcAnim.GetClipCount()}");
        }
    }
}