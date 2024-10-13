using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scripts
{
    [ExecuteInEditMode]
    public class DrawGizmosHand : MonoBehaviour
    {
        [HideInInspector] [SerializeField] public List<Transform> joints;
        [HideInInspector] public HandPoseHelper poseHelper;
        public List<List<Transform>> fingers;

        private int _countJoints
        {
            get
            {
                return fingers.Sum(finger => finger.Count);
            }
        }

        public IEnumerator StartAutoHand()
        {
            var isEnd = false;

            var edge = new float[_countJoints];
            
            while (!isEnd)
            {
                poseHelper.UndoRecordHands("Auto Pose");
                isEnd = true;
                for (var i = 0; i < fingers.Count;i++)
                {
                    var index = 0;
                    Transform activeFinger = null;
                    for (var j = fingers[i].Count - 1; j >= 0; j--)
                    {
                        if (!fingers[i][j].TryGetComponent(out Collider collider) || !collider.enabled) break;
                        index = i * fingers[i].Count + j;
                        activeFinger = fingers[i][j];
                        isEnd = false;
                    }

                    if (!activeFinger) continue;

                    if (edge[index] < poseHelper.maxRotate)
                        activeFinger.Rotate(poseHelper.speedRotate);
                    else
                        activeFinger.GetComponent<Collider>().enabled = false;

                    edge[index]++; 
                }
            
                yield return new WaitForFixedUpdate(); 
            }
        }
    }
    #if UNITY_EDITOR
    [CustomEditor(typeof(DrawGizmosHand))]
    public class DrawGizmosEditor : Editor
    {
        private DrawGizmosHand _hand;

        private float _defaultSize = 0.01f;

        private int _indexActiveJoint = -1;

        private void OnEnable()
        {
            _hand = (DrawGizmosHand)target;
            _hand.poseHelper = _hand.transform.parent.GetComponent<HandPoseHelper>();
            _hand.poseHelper.RebuildJoints();

            IdentifyFingers();
            IdentifyFingersColor();
        }

        private void OnSceneGUI()
        {
            _hand = (DrawGizmosHand)target;

            if (_hand.joints.Count == 0) return;

            DrawFingerBones();
            DrawJointButtons(_hand.joints);
            DrawJointHandle();
        }

        private void DrawJointButtons(List<Transform> joints)
        {
            for (var i = 0; i < joints.Count; i++)
            {
                _indexActiveJoint = DrawFingerJoints();
            }
        }

        private void DrawJointHandle()
        {
            // If a joint is selected
            if (HasActiveJoint())
            {
                // Draw handle
                Quaternion currentRotation = _hand.joints[_indexActiveJoint].rotation;
                Quaternion newRotation =
                    Handles.RotationHandle(currentRotation, _hand.joints[_indexActiveJoint].position);

                // Detect if handle has rotated
                if (HandleRotated(currentRotation, newRotation))
                {
                    Undo.RecordObject(_hand.joints[_indexActiveJoint], "Joint Rotated");
                    _hand.joints[_indexActiveJoint].rotation = newRotation;
                }
            }
        }

        private int DrawFingerJoints()
        {
            var index = 0;

            var sizeJoint = _defaultSize;

            if(!_hand.poseHelper.colorFingers.isForEachFinger)
                Handles.color = _hand.poseHelper.colorFingers.joints[0];

            for (var i = 0; i < _hand.fingers.Count; i++)
            {
                if(_hand.poseHelper.colorFingers.isForEachFinger)
                    Handles.color = _hand.poseHelper.colorFingers.joints[i];
                foreach (var joint in _hand.fingers[i])
                {
                    var pressed = Handles.Button(joint.position, joint.rotation, sizeJoint, 0.005f,
                        Handles.SphereHandleCap);

                    if (pressed) return index;

                    sizeJoint -= 0.0015f;
                    index++;
                }

                sizeJoint = _defaultSize;
            }

            return _indexActiveJoint;
        }

        private void DrawFingerBones()
        {
            Transform prevJoint = null;

            if(!_hand.poseHelper.colorFingers.isForEachFinger)
                UnityEditor.Handles.color = _hand.poseHelper.colorFingers.bones[0];

            for (var i = 0; i < _hand.fingers.Count; i++)
            {
                if(_hand.poseHelper.colorFingers.isForEachFinger)
                    UnityEditor.Handles.color = _hand.poseHelper.colorFingers.bones[i];
                foreach (var joint in _hand.fingers[i])
                {
                    if (prevJoint == null)
                    {
                        prevJoint = joint;
                        continue;
                    }

                    UnityEditor.Handles.DrawLine(prevJoint.position, joint.position, 5);
                    prevJoint = joint;
                }

                prevJoint = null;
            }
        }

        private bool HasActiveJoint()
        {
            return _indexActiveJoint > -1;
        }

        private bool HandleRotated(Quaternion currentRotation, Quaternion newRotation)
        {
            return currentRotation != newRotation;
        }

        private void IdentifyFingers()
        {
            var joints = new List<Transform>();
            var fingers = new List<List<Transform>>();

            for (var i = 0; i < _hand.joints.Count; i++)
            {
                if (_hand.joints[i].childCount != 0 && _hand.joints[i].GetChild(0) ==
                    _hand.joints[Math.Clamp(i + 1, 0, _hand.joints.Count - 1)])
                {
                    joints.Add(_hand.joints[i]);
                }
                else if (joints.Count > 0)
                {
                    joints.Add(_hand.joints[i]);
                    fingers.Add(new List<Transform>(joints));
                    joints.Clear();
                }
            }

            _hand.fingers = fingers;
        }

        private void IdentifyFingersColor()
        {
            var oldColorBones = new List<Color32>(_hand.poseHelper.colorFingers.bones);
            var oldColorJoints = new List<Color32>(_hand.poseHelper.colorFingers.joints);
            
            for (var i = 0; i < _hand.fingers.Count; i++)
            {
                if(i < oldColorBones.Count)
                    _hand.poseHelper.colorFingers.bones[i] = oldColorBones[i];
                else
                    _hand.poseHelper.colorFingers.bones.Add(Color.green);
                
                if(i < oldColorJoints.Count)
                    _hand.poseHelper.colorFingers.joints[i] = oldColorJoints[i];
                else
                    _hand.poseHelper.colorFingers.joints.Add(Color.yellow);
            }
            
            oldColorBones.Clear(); 
            oldColorJoints.Clear();
        }
    }
}
#endif