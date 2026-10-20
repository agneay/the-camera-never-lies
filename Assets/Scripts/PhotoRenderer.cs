using UnityEngine;

/// Renders real photographs from any pose into a Texture2D, using a dedicated off-screen camera.
public class PhotoRenderer : MonoBehaviour
{
    public static PhotoRenderer I;
    public Camera photoCam;

    public const int Width = 640;
    public const int Height = 360;

    RenderTexture rt;
    ViewfinderEffect fx;

    void Awake()
    {
        I = this;
        photoCam.enabled = false;
        rt = new RenderTexture(Width, Height, 24);
        photoCam.targetTexture = rt;
        photoCam.aspect = (float)Width / Height;
        fx = photoCam.GetComponent<ViewfinderEffect>();
    }

    public Texture2D Render(Vector3 pos, Quaternion rot, float fov, int mask)
    {
        photoCam.transform.SetPositionAndRotation(pos, rot);
        photoCam.fieldOfView = fov;
        photoCam.cullingMask = mask;
        if (fx) fx.seed = Random.value * 100f;
        photoCam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        return tex;
    }

    void OnDestroy()
    {
        if (rt) rt.Release();
    }
}
