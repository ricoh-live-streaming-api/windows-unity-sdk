using System.IO;
using UnityEngine;
using log4net.Config;
using log4net;

public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureLogging()
    {
        GlobalContext.Properties["PersistentDataPath"] = Application.persistentDataPath;

        XmlConfigurator.Configure(
            new FileInfo($"{Application.streamingAssetsPath}/Log4net.Config.xml")
        );
    }

}
