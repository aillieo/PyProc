using AillieoUtils;
using UnityEngine;

public class CS : MonoBehaviour
{
    private PyProc manager;

    private void OnEnable()
    {
        this.Create();
    }

    private void OnDisable()
    {
        this.Destroy();
    }

    [ContextMenu("Send")]
    private void Send()
    {
        if (this.manager == null)
        {
            this.Create();
        }

        var tsk = this.manager.SendAsync("https://www.google.com");
    }

    [ContextMenu("Create")]
    private void Create()
    {
        this.Destroy();

        this.manager = new PyProc(System.IO.Path.Combine(Application.dataPath, "PyProc/Sample/py.py"));

        this.manager.OnData += UnityEngine.Debug.Log;
        this.manager.OnOutput += UnityEngine.Debug.LogWarning;
        this.manager.OnError += UnityEngine.Debug.LogError;
    }

    [ContextMenu("Destroy")]
    private void Destroy()
    {
        if (this.manager == null)
        {
            return;
        }

        this.manager.Dispose();
        this.manager = null;
    }
}
