using UnityEngine;

/// Swaps a TextMesh onto a depth-tested material that tracks the dynamic font atlas.
[RequireComponent(typeof(TextMesh))]
public class DepthText : MonoBehaviour
{
    public Material template;

    static Material shared;

    void Awake()
    {
        var font = GetComponent<TextMesh>().font;
        if (shared == null && template != null)
        {
            shared = new Material(template);
            Font.textureRebuilt += f => { if (shared) shared.mainTexture = f.material.mainTexture; };
        }
        if (shared == null) return;
        shared.mainTexture = font.material.mainTexture;
        GetComponent<MeshRenderer>().sharedMaterial = shared;
    }
}
