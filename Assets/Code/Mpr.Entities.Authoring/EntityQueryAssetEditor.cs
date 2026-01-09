using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace Mpr.Blobs.Authoring;

[CustomEditor(typeof(EntityQueryAsset))]
public class EntityQueryAssetEditor : Editor
{
    private string oldValue;
    
    public override VisualElement CreateInspectorGUI()
    {
        var path = AssetDatabase.GetAssetPath(this.target);
        var value = File.ReadAllText((path));
        
        var text = new TextField();
        text.multiline = true;
        text.isDelayed = true;
        text.value = value;
        oldValue = value;
        text.RegisterValueChangedCallback(OnTextChanged);
        return text;
    }

    private void OnTextChanged(ChangeEvent<string> evt)
    {
        if (evt.newValue != oldValue)
        {
            var path = AssetDatabase.GetAssetPath(this.target);
            File.WriteAllText(path, evt.newValue);
            oldValue = evt.newValue;
            AssetDatabase.ImportAsset(path);
        }
    }
}