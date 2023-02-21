using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AnimationClipTool
{
    private string _path;
    
    private AnimationClip _clip;
    private int _indexRotate;

    public AnimationClipTool(string path = "Assets")
    {
        _path = path;
    }
    public void CreateClip(string name, GameObject target, List<Transform> listTransforms)
    {
        _indexRotate = 1;
        _clip = new AnimationClip {name = name, legacy = true};
        NextChildLevel(target, listTransforms);
        
        CreateAssetAnimClip(_path + "/" + _clip.name+".anim");
    }

    private void NextChildLevel(GameObject target, List<Transform> list, string relativePath = "")
    {
        foreach (Transform transform in target.transform)
        {
            if (_indexRotate >= list.Count)
            {
                Debug.LogError("Length list less than the number of children");
                return;
            }

            var slesh = relativePath == string.Empty ? "" : "/";
            var newRelativePass = relativePath + slesh + transform.name;
            SetRotate(newRelativePass, list[_indexRotate++].localRotation);

            if (transform.childCount > 0)
                NextChildLevel(transform.gameObject, list, newRelativePass);
        }
    }

    private void SetRotate(string relativePath, Quaternion value)
    {
        SetKey(relativePath, "localEuler.x", value.eulerAngles.x);
        SetKey(relativePath, "localEuler.y", value.eulerAngles.y);
        SetKey(relativePath, "localEuler.z", value.eulerAngles.z);
    }

    private void SetKey(string relativePath, string propertyName, float value)
    {
        AnimationCurve curve = new AnimationCurve(new Keyframe(1f, value));
        _clip.SetCurve(relativePath, typeof(Transform), propertyName, curve);
    }

    private void CreateAssetAnimClip(string path)
    {
        AnimationClip outputAnimClip = AssetDatabase.LoadMainAssetAtPath (path) as AnimationClip;
        if (outputAnimClip != null) {
            EditorUtility.CopySerialized (_clip, outputAnimClip);
            AssetDatabase.SaveAssets ();
        }
        else
        {
            outputAnimClip = new AnimationClip();
            EditorUtility.CopySerialized(_clip, outputAnimClip);
            AssetDatabase.CreateAsset(outputAnimClip, path);
        }
    }
}