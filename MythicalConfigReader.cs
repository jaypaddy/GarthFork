using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;



namespace MythicalIoTModuleDeploy
{
    public class MythicalConfigReader
    {


        public async Task<IDictionary<string, string>> GetCameraConfiguration(string deviceId) {

            try {

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return null;
        }
    }
}
