using System;
using System.Collections.Generic;
using HandPoseHelper.Scripts.Enums;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scripts
{
    [Serializable]
    public struct HandPose
    {
        public Vector3 attachPosition;
        public Quaternion attachRotation;
        public List<Quaternion> fingerRotations;
    }

    [Serializable]
    public struct HandPoseData
    {
        public string name;
        public HandPose leftHand;
        public HandPose rightHand;
    }

    [Serializable]
    public struct HandInfo
    {
        public List<string> indents;
        public List<Transform> values;
        public List<bool> toggles;

        public HandInfo(bool isAutoInit = true)
        {
            if (isAutoInit)
            {
                indents = new List<string>();
                values = new List<Transform>();
                toggles = new List<bool>();
                return;
            }

            indents = null;
            values = null;
            toggles = null;
        }

        public void Clear()
        {
            indents.Clear();
            values.Clear();
            toggles.Clear();
        }
    }

    [Serializable]
    public struct ColorFingers
    {
        public bool isForEachFinger;
        public List<Color32> bones;
        public List<Color32> joints;
    }

    [Serializable]
    public struct SaveDataSetting
    {
        public string path;
        public List<string> names;
        public List<MonoScript> templates;
    }

    [ExecuteInEditMode]
    public class HandPoseHelper : MonoBehaviour
    {
        [SerializeField] public bool isSelectParentHand = true;
        [HideInInspector] [SerializeField] public SaveDataTemplate defaultSaveData;
        [HideInInspector] [SerializeField] public bool isUseFingertip;
        [HideInInspector] [SerializeField] public Vector3 speedRotate;
        [HideInInspector] [SerializeField] public float maxRotate;
        [Space]
        [HideInInspector] public GameObject leftHandPrefab;
        [HideInInspector] public GameObject rightHandPrefab;
        [HideInInspector] [SerializeField] public List<Transform> leftHandJoints;
        [HideInInspector] [SerializeField] public List<Transform> rightHandJoints;

        [HideInInspector] [SerializeField] private GameObject _leftHand;
        [HideInInspector] [SerializeField] private GameObject _rightHand;

        private HandPoseData _clearHandPoseData;

        [HideInInspector] public HandInfo leftHandInfo;
        [HideInInspector] public HandInfo rightHandInfo;

        [HideInInspector] public ColorFingers colorFingers;

        [HideInInspector] public int popupIndexTemplate;
        [HideInInspector] public int popupIndexDefaultSaveData;
        [HideInInspector] public SaveDataSetting saveDataSetting;

        private Vector3 _offsetLeftHand;
        private Vector3 _offsetRightHand;

        private GameObject _mainSelectObject;

        private bool IsVisibleHands
        {
            set
            {
                _leftHand.SetActive(value);
                _rightHand.SetActive(value);
            }
            get => _leftHand.activeSelf && _rightHand.activeSelf;
        }

        private GameObject SetMainSelectObject
        {
            set
            {
                if (value != _mainSelectObject &&
                    value != gameObject &&
                    value != _leftHand &&
                    value != _rightHand)
                    _mainSelectObject = value;
            }
        }

        private List<GameObject> _mainSelectHands
        {
            get
            {
                List<GameObject> selectHands = new List<GameObject>();
                foreach (var obj in Selection.gameObjects)
                {
                    if (obj == _leftHand || obj == _rightHand)
                        selectHands.Add(obj);
                }

                return selectHands;
            }
        }

        public bool isDefaultPoseExist 
            => defaultSaveData &&
               (!defaultSaveData.GetIsUseDataCollection ||
                (defaultSaveData.GetIsUseDataCollection && defaultSaveData.GetAllNames.Count > 0));

        public void OnEnable()
        {
            if (!leftHandPrefab || !rightHandPrefab)
            {
                Debug.LogError("Add prefab for Left or/and Right hand");
                return;
            }

            if (transform.childCount > 0)
            {
                _leftHand = transform.GetChild(0).gameObject;
                _rightHand = transform.GetChild(1).gameObject;
            }
            else
            {
                _leftHand = Instantiate(leftHandPrefab, Vector3.zero, Quaternion.identity, transform);
                _rightHand = Instantiate(rightHandPrefab, Vector3.zero, Quaternion.identity, transform);

                _leftHand.name = "Left Hand";
                _rightHand.name = "Right Hand";

                _leftHand.AddComponent<DrawGizmosHand>().joints = leftHandJoints;
                _rightHand.AddComponent<DrawGizmosHand>().joints = rightHandJoints;

                if (leftHandInfo.indents.Count != leftHandInfo.values.Count &&
                    leftHandInfo.values.Count != leftHandInfo.toggles.Count)
                {
                    leftHandInfo.Clear();
                    rightHandInfo.Clear();
                    SetHandInfo(leftHandInfo, _leftHand.transform);
                    SetHandInfo(rightHandInfo, _rightHand.transform);
                }

                leftHandInfo.values.Clear();
                rightHandInfo.values.Clear();
                RefreshTransformJoints(leftHandInfo, _leftHand.transform);
                RefreshTransformJoints(rightHandInfo, _rightHand.transform);
                _clearHandPoseData = GetHandPoseData();
                ClearOrDefaultPose(HandType.All);
            }
        }

        private void Update()
        {
            if (!Selection.activeGameObject)
            {
                IsVisibleHands = false;
                return;
            }

            var selectObj = Selection.activeGameObject;

            if (isSelectParentHand) selectObj = SelectParentHand(selectObj);

            IsVisibleHands = true;
            if (_mainSelectObject != selectObj)
            {
                SetMainSelectObject = selectObj;
                transform.position = (_mainSelectObject) ? _mainSelectObject.transform.position : transform.position;
                SetOffsetHands();
            }
        }

        private void SetOffsetHands()
        {
            _offsetLeftHand = _leftHand.transform.position - transform.position;
            _offsetRightHand = _rightHand.transform.position - transform.position;
        }

        private void SetPositionHands()
        {
            _leftHand.transform.position = transform.position + _offsetLeftHand;
            _rightHand.transform.position = transform.position + _offsetRightHand;
        }

        private GameObject SelectParentHand(GameObject selectObject)
        {
            var parent = selectObject.transform.parent;

            if (!parent) return Selection.activeGameObject;

            if (selectObject != _leftHand && selectObject != _rightHand)
            {
                Selection.activeObject = SelectParentHand(parent.gameObject);
            }
            else
            {
                return selectObject;
            }

            return Selection.activeGameObject;
        }

        public void SetHandInfo(HandInfo handInfo, Transform transform, string space = "├",
            string line = "")
        {
            space = transform.GetSiblingIndex() == transform.parent?.childCount - 1
                ? space.Replace("├", "└")
                : space.Replace("└", "├");

            handInfo.indents.Add(transform.parent ? space : "    ");
            handInfo.values.Add(transform);
            handInfo.toggles.Add(false);

            if (!UseVerticalLine(transform, ref space, ref line))
                space = "      " + space;

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                SetHandInfo(handInfo, child, space, line);
            }
        }

        private bool UseVerticalLine(Transform transform, ref string space, ref string line)
        {
            var isNewLine = transform.childCount > 0 && transform.GetSiblingIndex() < transform.parent?.childCount - 1;

            if (isNewLine || line.Length > 0)
            {
                line = (isNewLine) ? "│  " : "      ";

                if (space.Contains("└"))
                    space = space.Split("└")[0] + line + "└";
                else if (space.Contains("├"))
                    space = space.Split("├")[0] + line + "├";

                return true;
            }

            return false;
        }

        public void UndoRecordHands(string name, HandType handType = HandType.All)
        {
            var objects = new List<Object>();
            if (handType != HandType.Right && handType != HandType.None)
            {
                objects.Add(_leftHand.transform);
                objects.AddRange(leftHandInfo.values);
            }

            if (handType != HandType.Left && handType != HandType.None)
            {
                objects.Add(_rightHand.transform);
                objects.AddRange(rightHandInfo.values);
            }

            Undo.RecordObjects(objects.ToArray(), name);
        }

        public void RefreshTransformJoints(HandInfo handInfo, Transform newValue)
        {
            handInfo.values.Add(newValue);

            for (var i = 0; i < newValue.childCount; i++)
            {
                var child = newValue.GetChild(i);
                RefreshTransformJoints(handInfo, child);
            }
        }

        public void RebuildJoints()
        {
            leftHandJoints.Clear();
            rightHandJoints.Clear();

            for (var i = 0; i < leftHandInfo.toggles.Count; i++)
            {
                if (leftHandInfo.toggles[i])
                    leftHandJoints.Add(leftHandInfo.values[i]);
            }

            for (var i = 0; i < rightHandInfo.toggles.Count; i++)
            {
                if (rightHandInfo.toggles[i])
                    rightHandJoints.Add(rightHandInfo.values[i]);
            }
        }

        public HandPoseData GetHandPoseData()
        {
            var handPoseData = new HandPoseData();

            handPoseData.leftHand.attachPosition = _offsetLeftHand;
            handPoseData.rightHand.attachPosition = _offsetRightHand;

            handPoseData.leftHand.fingerRotations = new List<Quaternion>();
            foreach (var transform in leftHandInfo.values)
            {
                handPoseData.leftHand.fingerRotations.Add(transform.rotation);
            }

            handPoseData.rightHand.fingerRotations = new List<Quaternion>();
            foreach (var transform in rightHandInfo.values)
            {
                handPoseData.rightHand.fingerRotations.Add(transform.rotation);
            }

            return handPoseData;
        }

        public void SetHandPoseData(HandPoseData handPoseData, HandType handType = HandType.All)
        {
            if (handType is HandType.All or HandType.Left)
            {
                _offsetLeftHand = handPoseData.leftHand.attachPosition;
                for (var i = 0; i < handPoseData.leftHand.fingerRotations.Count; i++)
                {
                    leftHandInfo.values[i].rotation = handPoseData.leftHand.fingerRotations[i];
                }
            }

            if (handType is HandType.All or HandType.Right)
            {
                _offsetRightHand = handPoseData.rightHand.attachPosition;
                for (var i = 0; i < handPoseData.rightHand.fingerRotations.Count; i++)
                {
                    rightHandInfo.values[i].rotation = handPoseData.rightHand.fingerRotations[i];
                }
            }

            SetPositionHands();
        }

        public void ClearOrDefaultPose(HandType handType)
        {
            if (handType == HandType.None) return;
            
            UndoRecordHands("Clear or default hands");

            if (isDefaultPoseExist)
            {
                if (defaultSaveData.GetAllNames?.Count > 0)
                {
                    var name = defaultSaveData.GetAllNames?[popupIndexDefaultSaveData];
                    var handPoseData = defaultSaveData.Load(name);
                    _leftHand.transform.localPosition = handPoseData.leftHand.attachPosition;
                    _rightHand.transform.localPosition = handPoseData.rightHand.attachPosition; 
                    SetHandPoseData(handPoseData, handType);
                    return;
                }
                _leftHand.transform.localPosition = defaultSaveData.Load().leftHand.attachPosition;
                _rightHand.transform.localPosition = defaultSaveData.Load().rightHand.attachPosition; 
                SetHandPoseData(defaultSaveData.Load(), handType);
            }
            else
            {
                _leftHand.transform.localPosition = Vector3.zero;
                _rightHand.transform.localPosition = Vector3.zero;
                SetHandPoseData(_clearHandPoseData, handType);
            }
        }

        public void MirrorPose(bool isLeftHand)
        {
            UndoRecordHands("Mirror hand");

            if (isLeftHand)
            {
                var pos = _leftHand.transform.localPosition;
                var rot = _leftHand.transform.localRotation;
                pos.x *= -1f;
                rot.z *= -1f;
                rot.y *= -1f;
                _rightHand.transform.localPosition = pos;
                _rightHand.transform.localRotation = rot;
            }
            else
            {
                var pos = _rightHand.transform.localPosition;
                var rot = _rightHand.transform.localRotation;
                pos.x *= -1f;
                rot.z *= -1f;
                rot.y *= -1f;
                _leftHand.transform.localPosition = pos;
                _leftHand.transform.localRotation = rot;
            }

            var originHandInfo = (isLeftHand) ? leftHandInfo : rightHandInfo;
            var newHandInfo = (isLeftHand) ? rightHandInfo : leftHandInfo;

            for (var i = 0; i < newHandInfo.values.Count; i++)
            {
                if (!newHandInfo.toggles[i]) continue;

                Quaternion mirrorRotation = originHandInfo.values[i].localRotation;
                mirrorRotation.x *= -1.0f;
                mirrorRotation.z *= -1.0f;

                newHandInfo.values[i].localRotation = mirrorRotation;
            }
        }

        public void AutoPose()
        {
            InitMainSelectAutoPose();

            foreach (var selectHand in _mainSelectHands)
            {
                var hand = selectHand.GetComponent<DrawGizmosHand>();

                InitHandAutoPose(hand.fingers);
                EditorCoroutineUtility.StartCoroutine(hand.StartAutoHand(), this);
            }
        }

        private void InitMainSelectAutoPose()
        {
            if (!_mainSelectObject.TryGetComponent(out Collider collider))
            {
                if (!_mainSelectObject.TryGetComponent(out MeshFilter selectMeshFilter))
                {
                    Debug.LogError(
                        "Add to selected object " + _mainSelectObject.name + " mesh filter or collide.");
                    return;
                }

                var meshCollider = _mainSelectObject.AddComponent<MeshCollider>();
                meshCollider.convex = true;
                collider = meshCollider;
            }

            collider.isTrigger = true;

            if (!_mainSelectObject.TryGetComponent(out Rigidbody rigidbody))
            {
                rigidbody = _mainSelectObject.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            if (!_mainSelectObject.TryGetComponent(out TriggerForFinger script))
            {
                _mainSelectObject.AddComponent<TriggerForFinger>();
            }
        }

        private void InitHandAutoPose(List<List<Transform>> fingers)
        {
            foreach (var finger in fingers)
            {
                for (var i = 0; i < finger.Count - 1; i++)
                {
                    CreateBoneCollider(finger[i], finger[i + 1]);
                }

                if (isUseFingertip) CreateBoneCollider(finger[^1], 0.025f);
            }
        }

        private void CreateBoneCollider(Transform joint, float height)
        {
            if (!joint.gameObject.TryGetComponent(out CapsuleCollider collider))
                collider = joint.gameObject.AddComponent<CapsuleCollider>();
            collider.enabled = true;
            collider.radius = 0.01f;
            collider.direction = 0;
            collider.height = height;
            var newVector = Vector3.zero;
            newVector[collider.direction] = collider.height / 2f;
            collider.center = -newVector;
        }

        private void CreateBoneCollider(Transform joint1, Transform joint2)
        {
            if (!joint1.gameObject.TryGetComponent(out CapsuleCollider collider))
                collider = joint1.gameObject.AddComponent<CapsuleCollider>();

            collider.enabled = true;
            collider.radius = 0.01f;

            var offsetVector = (joint2.position - joint1.position).normalized;
            var axis = -1;
            var dotValue = new Vector3(Mathf.RoundToInt(Vector3.Dot(joint1.right, offsetVector)),
                Mathf.RoundToInt(Vector3.Dot(joint1.up, offsetVector)),
                Mathf.RoundToInt(Vector3.Dot(joint1.forward, offsetVector)));

            axis = (dotValue[0] != 0) ? 0 : axis;
            axis = (dotValue[1] != 0) ? 1 : axis;
            axis = (dotValue[2] != 0) ? 2 : axis;
            collider.direction = axis;

            collider.height = Vector3.Distance(joint1.position, joint2.position);
            var newVector = Vector3.zero;
            newVector[collider.direction] = collider.height / 2f;
            collider.center = newVector * dotValue[axis];
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(HandPoseHelper))]
    public class HandPoseHelperEditor : Editor
    {
        private HandPoseHelper _handPoseHelper;

        private SerializedProperty _leftHandPrefab;
        private SerializedProperty _rightHandPrefab;
        private SerializedProperty _defaultSaveData;
        private SerializedProperty _popupIndexDefaultSaveData;
        private SerializedProperty _isUseFingertip;
        private SerializedProperty _speedRotate;
        private SerializedProperty _maxRotate;

        private Transform _activeJoint;

        private bool _isFoldOutHandOptions;
        private bool _isFoldOutAutoHandOption;
        private bool _isFoldOutSaveDataOptions;
        private bool _isDisplayOneHand;
        private bool _isRootPrefab;

        private void OnEnable()
        {
            _handPoseHelper = (HandPoseHelper)target;

            _defaultSaveData = serializedObject.FindProperty("defaultSaveData");
            _popupIndexDefaultSaveData = serializedObject.FindProperty("popupIndexDefaultSaveData");
            _isUseFingertip = serializedObject.FindProperty("isUseFingertip");
            _speedRotate = serializedObject.FindProperty("speedRotate");
            _maxRotate = serializedObject.FindProperty("maxRotate");

            _isRootPrefab = !PrefabUtility.GetCorrespondingObjectFromSource(_handPoseHelper.gameObject);

            if (_isRootPrefab)
            {
                _leftHandPrefab = serializedObject.FindProperty("leftHandPrefab");
                _rightHandPrefab = serializedObject.FindProperty("rightHandPrefab");
            }

            if (!_handPoseHelper.leftHandPrefab || !_handPoseHelper.rightHandPrefab)
            {
                Debug.LogError("Add prefab for Left or/and Right hand");
                return;
            }

            serializedObject.Update();

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            _handPoseHelper = (HandPoseHelper)target;

            if (_isRootPrefab)
            {
                EditorGUILayout.PropertyField(_leftHandPrefab);
                EditorGUILayout.PropertyField(_rightHandPrefab);
                EditorGUILayout.Space();

                _isFoldOutSaveDataOptions =
                    EditorGUILayout.Foldout(_isFoldOutSaveDataOptions, "Setting Save Data", true);

                if (_isFoldOutSaveDataOptions)
                {
                    SettingSaveData();
                }

                serializedObject.ApplyModifiedProperties();
                return;
            }

            base.OnInspectorGUI();

            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_defaultSaveData);
            if(EditorGUI.EndChangeCheck())
            {
                _popupIndexDefaultSaveData.intValue = 0;
            }
            if (((SaveDataTemplate)_defaultSaveData.objectReferenceValue)?.GetAllNames?.Count > 0)
                _popupIndexDefaultSaveData.intValue = EditorGUILayout.Popup(_popupIndexDefaultSaveData.intValue,
                    ((SaveDataTemplate)_defaultSaveData.objectReferenceValue).GetAllNames.ToArray(), GUILayout.Width(100f));
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            _isFoldOutHandOptions = EditorGUILayout.Foldout(_isFoldOutHandOptions, "Options fingers hand", true);
            _isDisplayOneHand = GUILayout.Toggle(_isDisplayOneHand, " 2 in 1",
                GUILayout.Width(Screen.width - EditorGUIUtility.labelWidth - 25));
            GUILayout.EndHorizontal();
            _isFoldOutHandOptions = _isDisplayOneHand || _isFoldOutHandOptions;

            if (_isFoldOutHandOptions)
            {
                _handPoseHelper.RebuildJoints();

                if (_isDisplayOneHand)
                {
                    Show2in1();
                }
                else
                {
                    ShowHierarchyHand(_handPoseHelper.leftHandInfo);
                    ShowHierarchyHand(_handPoseHelper.rightHandInfo);
                }
            }

            EditorGUILayout.Space();
            _isFoldOutAutoHandOption = EditorGUILayout.Foldout(_isFoldOutAutoHandOption, "Options auto hand", true);

            if (_isFoldOutAutoHandOption)
            {
                EditorGUILayout.PropertyField(_isUseFingertip);
                EditorGUILayout.PropertyField(_speedRotate);
                _maxRotate.floatValue = EditorGUILayout.Slider("Edge Rotate", _maxRotate.floatValue, 30, 100);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void SettingSaveData()
        {
            var saveDataSetting = serializedObject.FindProperty("saveDataSetting");
            var names = saveDataSetting.FindPropertyRelative("names");
            var templates = saveDataSetting.FindPropertyRelative("templates");
            var path = saveDataSetting.FindPropertyRelative("path");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUI.enabled = false;
            EditorGUILayout.TextField(path.stringValue);
            GUI.enabled = true;
            if (GUILayout.Button("New Save Path"))
            {
                string newPath = EditorUtility.OpenFolderPanel("Folder save data ", "", "");
                if (newPath.Contains("Assets"))
                    newPath = "Assets" + newPath.Split("Assets")[1];
                path.stringValue = newPath;
                return;
            }

            GUILayout.Space(22);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            for (var i = 0; i < names.arraySize; i++)
            {
                var name = names.GetArrayElementAtIndex(i);
                var template = templates.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(name, new GUIContent(""));
                EditorGUILayout.PropertyField(template, new GUIContent(""));
                if (GUILayout.Button("X"))
                {
                    names.DeleteArrayElementAtIndex(i);
                    templates.DeleteArrayElementAtIndex(i);
                    GUI.FocusControl(null);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button("Add"))
            {
                names.arraySize++;
                templates.arraySize++;
            }

            GUILayout.Space(22);
            EditorGUILayout.EndHorizontal();
        }


        private bool AddElementHand(string indent, string name, bool toggle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(indent, GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent(indent)).x));
            var result = GUILayout.Toggle(toggle, name);
            GUILayout.EndHorizontal();

            return result;
        }

        private void ShowHierarchyHand(HandInfo handInfo)
        {
            for (var i = 0; i < handInfo.values.Count; i++)
            {
                var indent = handInfo.indents[i];
                var value = (Transform)handInfo.values[i];
                var toggle = handInfo.toggles[i];

                handInfo.toggles[i] = AddElementHand(indent, value.name, toggle);
            }
        }

        private void Show2in1()
        {
            if (_handPoseHelper.leftHandInfo.values.Count != _handPoseHelper.rightHandInfo.values.Count)
            {
                Debug.LogError("Left and Right hand have a different number of elements");
                _isDisplayOneHand = false;
                return;
            }

            for (var i = 0; i < _handPoseHelper.leftHandInfo.values.Count; i++)
            {
                var indent = _handPoseHelper.leftHandInfo.indents[i].Remove(0, 1);
                var toggle = _handPoseHelper.leftHandInfo.toggles[i] && _handPoseHelper.rightHandInfo.toggles[i];
                var name = _handPoseHelper.leftHandInfo.values[i].name == _handPoseHelper.rightHandInfo.values[i].name
                    ? _handPoseHelper.leftHandInfo.values[i].name
                    : _handPoseHelper.leftHandInfo.values[i].name + " | " +
                      _handPoseHelper.rightHandInfo.values[i].name;

                var temporaryToggle = AddElementHand(indent, name, toggle);

                _handPoseHelper.leftHandInfo.toggles[i] = temporaryToggle;
                _handPoseHelper.rightHandInfo.toggles[i] = temporaryToggle;
            }
        }
    }
}
#endif