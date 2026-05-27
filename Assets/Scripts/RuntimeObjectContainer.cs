using UnityEngine;

public static class RuntimeObjectContainer
{
    private const string RootName = "Runtime Objects";

    private static Transform _root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _root = null;
    }

    public static Transform Get(string containerName)
    {
        Transform root = GetRoot();
        Transform container = root.Find(containerName);

        if (container != null)
            return container;

        GameObject containerObject = new GameObject(containerName);
        containerObject.transform.SetParent(root, false);
        return containerObject.transform;
    }

    private static Transform GetRoot()
    {
        if (_root != null)
            return _root;

        GameObject rootObject = GameObject.Find(RootName);

        if (rootObject == null)
            rootObject = new GameObject(RootName);

        _root = rootObject.transform;
        return _root;
    }
}
