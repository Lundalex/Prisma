using UnityEngine;
using Object = UnityEngine.Object;

namespace LeTai.Asset.TranslucentImage
{
[CreateAssetMenu(menuName = "Translucent Image/Default Resources")]
public class DefaultResources : ScriptableObject
{
    static DefaultResources instance;

    public static DefaultResources Instance
    {
        get
        {
            if (!instance)
            {
                var source = Resources.Load<DefaultResources>("Translucent Image Default Resources");
                instance                  = MakeTempCopy(source);
                instance.material         = MakeTempCopy(instance.material);
                instance.paraformMaterial = MakeTempCopy(instance.paraformMaterial);
            }
            return instance;
        }
    }

    public Material material;
    public Material paraformMaterial;

    static T MakeTempCopy<T>(T obj) where T : Object
    {
        T copy = Instantiate(obj);
        copy.name      = obj.name;
        copy.hideFlags = HideFlags.HideAndDontSave;
        return copy;
    }
}
}
