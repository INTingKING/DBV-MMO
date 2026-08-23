using UnityEngine;

public static class RuntimeSingleton
{
    public static T Ensure<T>(string objectName) where T : MonoBehaviour
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject(string.IsNullOrEmpty(objectName) ? typeof(T).Name : objectName);
        return go.AddComponent<T>();
    }
}
