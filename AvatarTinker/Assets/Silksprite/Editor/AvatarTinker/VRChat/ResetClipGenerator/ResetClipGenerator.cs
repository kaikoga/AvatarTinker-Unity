#if VRC_SDK_VRCSDK3

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Silksprite.AvatarTinker.VRChat.ResetClipGenerator
{
    [Serializable]
    public class ResetClipGenerator
    {
        [SerializeField] public VRCAvatarDescriptor avatarDescriptor;
        [SerializeField] public List<AnimationClip> resetClips;

        public void FindResetClips()
        {
            resetClips = CollectAnimatorControllers().SelectMany(animatorController => CollectClips(animatorController).Take(1)).ToList();
        }

        public void GenerateResetClips()
        {
            var clipCaches = CollectAnimatorControllers().ToDictionary(animatorController => animatorController, CollectClips);
            
            foreach (var resetClip in resetClips)
            {
                if (resetClip == null) continue;
                var animatorController = clipCaches.FirstOrDefault(clipCache => clipCache.Value.Contains(resetClip)).Key;
                if (animatorController == null) continue;

                var clips = CollectClips(animatorController).Where(clip => clip != resetClip).ToArray();
                GenerateResetClip(clips, resetClip);
            }
        }

        IEnumerable<AnimatorController> CollectAnimatorControllers()
        {
            var layers = Enumerable.Empty<VRCAvatarDescriptor.CustomAnimLayer>()
                .Concat(avatarDescriptor.baseAnimationLayers)
                .Concat(avatarDescriptor.specialAnimationLayers);
            return layers.Select(layer => layer.animatorController).OfType<AnimatorController>();
        }

        static IEnumerable<AnimationClip> CollectClips(AnimatorController animatorController)
        {
            IEnumerable<AnimatorStateMachine> VisitStateMachines(AnimatorStateMachine stateMachine)
            {
                yield return stateMachine;
                foreach (var child in stateMachine.stateMachines)
                {
                    foreach (var sm in VisitStateMachines(child.stateMachine)) yield return sm;
                }
            }
            var stateMachines = animatorController.layers.SelectMany(layer => VisitStateMachines(layer.stateMachine)).Distinct();
            var states = stateMachines.SelectMany(stateMachine => stateMachine.states.Select(childState => childState.state)).Distinct();
            var motions = states.Select(state => state.motion).Distinct();
            IEnumerable<AnimationClip> VisitClips(Motion motion)
            {
                switch (motion)
                {
                    case AnimationClip clip:
                        yield return clip;
                        break;
                    case BlendTree blendTree:
                        foreach (var child in blendTree.children)
                        {
                            foreach (var childClip in VisitClips(child.motion)) yield return childClip;
                        }
                        break;
                }
            }
            return motions.SelectMany(VisitClips).Distinct();
        }

        void GenerateResetClip(AnimationClip[] sourceClips, AnimationClip targetClip)
        {
            var curveBindings = sourceClips.SelectMany(AnimationUtility.GetCurveBindings)
                .Distinct().OrderBy(curve => (curve.path, curve.propertyName, curve.type));
            var objectReferenceCurveBindings = sourceClips.SelectMany(AnimationUtility.GetObjectReferenceCurveBindings)
                .Distinct().OrderBy(curve => (curve.path, curve.propertyName, curve.type));
            
            var avatar = avatarDescriptor != null ? avatarDescriptor.gameObject : null;

            targetClip.ClearCurves();
            targetClip.frameRate = 60f;
            foreach (var curveBinding in curveBindings)
            {
                var value = 0f;
                if (avatar)
                {
                    AnimationUtility.GetFloatValue(avatar, curveBinding, out value);
                }
                targetClip.SetCurve(curveBinding.path, curveBinding.type, curveBinding.propertyName, AnimationCurve.Constant(0f, 1 / 60f, value));
            }
            foreach (var curveBinding in objectReferenceCurveBindings)
            {
                Object value = null;
                if (avatar)
                {
                    AnimationUtility.GetObjectReferenceValue(avatar, curveBinding, out value);
                }
                AnimationUtility.SetObjectReferenceCurve(targetClip, curveBinding, new []
                {
                    new ObjectReferenceKeyframe { time = 0, value = value },
                    new ObjectReferenceKeyframe { time = 1 / 60f, value = value }
                });
            }
        }
    }
}
#endif
