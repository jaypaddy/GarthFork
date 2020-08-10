using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using System.IO;


namespace MythicalIoTModuleDeploy
{

    class Program
    {
        private static string s_connectionString = Environment.GetEnvironmentVariable("IOTHUB_CONN_STRING_CSHARP");
        private static string s_acruser = Environment.GetEnvironmentVariable("ACRUSER");
        private static string s_acrpassword = Environment.GetEnvironmentVariable("ACRPASSWORD");
        private static string s_acr = Environment.GetEnvironmentVariable("ACR");


        public static async Task<int> Main(string[] args)
        {
            if (string.IsNullOrEmpty(s_connectionString) && args.Length > 0)
            {
                s_connectionString = args[0];
            }


            using RegistryManager registryManager = RegistryManager.CreateFromConnectionString(s_connectionString);
            Device homeDevice = await registryManager.GetDeviceAsync("alpha");

            string moduleJSON; 

            using (var sr = new StreamReader("./template/modules.json"))
            {
                // Read the stream as a string, and write the string to the console.
                moduleJSON = sr.ReadToEnd();
            }

            //Replace {$ACRUSER} {$ACRPASSWORD} {$ACR}
            moduleJSON = moduleJSON.Replace("{$ACRUSER}",s_acruser).Replace("{$ACRPASSWORD}",s_acrpassword).Replace("{$ACR}",s_acr);
            MythicalIoTModuleDeploy modDeploy = new MythicalIoTModuleDeploy(registryManager);

            //var ret2 = await modDeploy.Deploy("home", moduleJSON);
            //Boolean b = await modDeploy.DeployUsingTags("tags.location.building='20'",moduleJSON );
            //var ret = await modDeploy.DeployUsingModuleIterator("alpha", moduleJSON,"PySendModule",1);
            var ret = await modDeploy.DeployUsingTagsModuleIterator("tags.location.building='20'",moduleJSON,"PySendModule",2);

           /* var ret2 = await modDeploy.Deploy("home", moduleJSON);
            var obj1 = await modDeploy.DeployUsingModuleIterator("","","",0);
            var obj2 = await modDeploy.DeployUsingTagsModuleIterator("","","",0);
            */

            return 0;
        }
    }
}
