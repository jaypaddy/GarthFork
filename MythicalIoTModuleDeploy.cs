using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;



namespace MythicalIoTModuleDeploy
{
    public class MythicalIoTModuleDeploy
    {
        private readonly RegistryManager _registryManager;
        
        // Maximum number of elements per query.
        private const int QueryPageSize = 100;

        public MythicalIoTModuleDeploy(RegistryManager registryManager)
        {
            _registryManager = registryManager ?? throw new ArgumentNullException(nameof(registryManager));
        }



        /*Query Devices*/
        public async Task<IDictionary<string, Twin>> GetDevicesByTagFilter(string tagFilter) {

            IDictionary<string,Twin> devices = new Dictionary<string,Twin>();

            try {
                tagFilter = tagFilter ?? throw new ArgumentNullException(nameof(tagFilter));
                //Query for the given devices
                string query = $"select * From devices where {tagFilter}";
                var queryResults = _registryManager.CreateQuery(query, QueryPageSize);
                while (queryResults.HasMoreResults)
                {
                    IEnumerable<Twin> twins = await queryResults.GetNextAsTwinAsync().ConfigureAwait(false);
                    foreach (Twin twin in twins)
                    {
                        devices.Add(twin.DeviceId,twin);
                        Console.WriteLine(
                            "\t{0, -50} : {1, 10} : Last seen: {2, -10}",
                            twin.DeviceId,
                            twin.ConnectionState,
                            twin.LastActivityTime);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e}");
            }

            return devices;
        }


        /* Deploy specified Manifest to the specified deviceId */
        public async Task<Boolean> Deploy(string deviceid, string modulesTemplate)
        {
            /*
                AddConfigurationContentOnDeviceAsync (modulesTemplate)
            */

            try {
                deviceid = deviceid ?? throw new ArgumentNullException(nameof(deviceid));
                Device targetDevice = await _registryManager.GetDeviceAsync(deviceid);
                //Check if the Device exists
                targetDevice = targetDevice ?? throw new ArgumentNullException(nameof(targetDevice));
                //Get all the Modules
                //Convert baseTemplate to Dictionary<String,Object> 
                //Read the entire modulesTemplate
                IDictionary<string,object> modulesContentObj = JsonConvert.DeserializeObject<IDictionary<string,object>>(modulesTemplate);
                //What we essentially have is Dict<"ModulesContent",Object>
                //Transform ModulesContentObject into Dictionary<String,Object>
                string modulesObjStr = JsonConvert.SerializeObject(modulesContentObj["modulesContent"]);
                IDictionary<string,object> modulesContentObj2 = JsonConvert.DeserializeObject<IDictionary<string,object>>(modulesObjStr);
                //Now build the ConfigurationContent IDictionary<String,IDictionary<String,Object>>
                IDictionary<string,IDictionary<string,object>> ModulesContent = new Dictionary<string,IDictionary<string,object>>();
                IDictionary<string, IDictionary<string,object>> modulesContentConfigContent = new Dictionary<string, IDictionary<string,object>>();
                foreach (var item in modulesContentObj2)
                {
                    //Get the Key
                    //This will be the module names, $edgeAgent, $edgeHub - System Modules & Custom Modules
                    string key = item.Key;
                    //The Value is an object - properties.desired
                    //Grab this from 
                    string desiredPropertiesStr = JsonConvert.SerializeObject(modulesContentObj2[key]);
                    //Break it further to level 3 IDictionary<string,object>
                    IDictionary<string,object> modulesContentObj3 = JsonConvert.DeserializeObject<IDictionary<string,object>>(desiredPropertiesStr);
                    modulesContentConfigContent.Add(key,modulesContentObj3);
                }
                ConfigurationContent modConfigContent = new ConfigurationContent();
                modConfigContent.ModulesContent = modulesContentConfigContent;
                Console.WriteLine($"Applying to Device {deviceid} configuration {JsonConvert.SerializeObject(modulesContentConfigContent)}");
                var modOnConfigTask =  _registryManager.ApplyConfigurationContentOnDeviceAsync(deviceid,modConfigContent);
                await Task.WhenAll(modOnConfigTask).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
            return true;
        }
    
        /* Deploy specified Manifest to specified device, with module replica count and desired properties */
        public async Task<Boolean> DeployUsingModuleIterator(string deviceid, string modulesTemplate, string iterateModule, int nIterationCount) {

            /*
            Get properties.desired for $edgeAgent
                Break it into <string,object> : <modules,<moduledefinition>>
                Find the iterateModule and grab the object
                Break it into <string,object> : <iterateModule,<moduledefinition>>
                Loop until nIterationCount
                    Create a new Key <ierateModule><nIndex>
                    Insert <Key, moduledefinition>

            */
            try {
                deviceid = deviceid ?? throw new ArgumentNullException(nameof(deviceid));
                Device targetDevice = await _registryManager.GetDeviceAsync(deviceid);
                //Check if the Device exists
                targetDevice = targetDevice ?? throw new ArgumentNullException(nameof(targetDevice));

                //Get all the Modules
                //Convert modules Template to Dictionary<String,Object>  [LEVEL 1]
                //Read the entire modulesTemplate
                IDictionary<string,object> modulesContentObj = JsonConvert.DeserializeObject<IDictionary<string,object>>(modulesTemplate);

                //What we essentially have is Dict<"ModulesContent",Object>
                //Transform ModulesContentObject into Dictionary<String,Object> [LEVEL 2]
                string modulesObjStr = JsonConvert.SerializeObject(modulesContentObj["modulesContent"]);
                IDictionary<string,object> modulesContentObj2 = JsonConvert.DeserializeObject<IDictionary<string,object>>(modulesObjStr);
                
                //Transform $edgeAgent intoo Dictionary<Sring,Object> [LEVEL 3]
                string edgeAgentObjStr = JsonConvert.SerializeObject(modulesContentObj2["$edgeAgent"]);
                IDictionary<string,object> edgeAgentDesiredPropertiesObj = JsonConvert.DeserializeObject<IDictionary<string,object>>(edgeAgentObjStr);
                string edgeAgentDesiredPropertiesObjStr = JsonConvert.SerializeObject(edgeAgentDesiredPropertiesObj["properties.desired"]);

                //Transform $edgeAgent into modules - Dictionary<String,Object> [LEVEL 4]
                IDictionary<string,object> modulesObj = JsonConvert.DeserializeObject<IDictionary<string,object>>(edgeAgentDesiredPropertiesObjStr);
                string customModulesObjStr = JsonConvert.SerializeObject(modulesObj["modules"]);

                //Transform $edgeAgent into modules - Dictionary<String,Object> [LEVEL 4]
                IDictionary<string,object> customModulesObj = JsonConvert.DeserializeObject<IDictionary<string,object>>(customModulesObjStr);

                for (var idx=1;idx<=nIterationCount; idx++){
                    string moduleName = $"{iterateModule}{idx}";
                    customModulesObj.Add(moduleName,customModulesObj[iterateModule]);
                }

                //Replace modulesObj["modules"] with customModulesObj
                modulesObj["modules"] = (object) customModulesObj;
                //Replace edgeAgentDesiredPropertiesObj["properties.desired"] with modulesObj
                edgeAgentDesiredPropertiesObj["properties.desired"] = modulesObj;
                //Replace modulesContentObj2 with edgeAgentDesiredPropertiesObj
                modulesContentObj2["$edgeAgent"] = edgeAgentDesiredPropertiesObj;

                //We are done with $edgeAgent
                //Now we focus on Desired Properties for the iterator module
                //Loop through modulesContentObj2 and find the iterator module desired properties
                string customModuleDesiredPropertiesObjStr = JsonConvert.SerializeObject(modulesContentObj2[iterateModule]);
                for (var idx=1;idx<=nIterationCount; idx++){
                    string moduleName = $"{iterateModule}{idx}";
                    modulesContentObj2.Add(moduleName,modulesContentObj2[iterateModule]);
                    //Replace values for one of the tags
                }                

                IDictionary<string, IDictionary<string,object>> modulesContentConfigContent = new Dictionary<string, IDictionary<string,object>>();
                foreach (var item in modulesContentObj2)
                {
                    //Get the Key
                    //This will be the module names, $edgeAgent, $edgeHub - System Modules & Custom Modules
                    string key = item.Key;
                    //The Value is an object - properties.desired
                    //Grab this from 
                    string desiredPropertiesStr = JsonConvert.SerializeObject(modulesContentObj2[key]);
                    //Break it further to level 3 IDictionary<string,object>
                    IDictionary<string,object> modulesContentObj3 = JsonConvert.DeserializeObject<IDictionary<string,object>>(desiredPropertiesStr);
                    modulesContentConfigContent.Add(key,modulesContentObj3);
                }
                ConfigurationContent modConfigContent = new ConfigurationContent();
                modConfigContent.ModulesContent = modulesContentConfigContent;
                Console.WriteLine($"Applying to Device {deviceid} configuration");
                Console.WriteLine($"{JsonConvert.SerializeObject(modulesContentConfigContent)}");
                var modOnConfigTask =  _registryManager.ApplyConfigurationContentOnDeviceAsync(deviceid,modConfigContent);
                await Task.WhenAll(modOnConfigTask).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return true;
        }
    
        /* Deploy specified Manifest to devices satisfying the tagFilter */
        public async Task<Boolean> DeployUsingTags(string tagFilter, string modulesTemplate) {

            IDictionary<string,Twin> devices;
            try {
                tagFilter = tagFilter ?? throw new ArgumentNullException(nameof(tagFilter));
                devices = await GetDevicesByTagFilter(tagFilter);
                //concurrency???
                foreach (var d in devices) {
                    Boolean b = await Deploy(d.Key,modulesTemplate);
                    if (b){
                        Console.WriteLine($"Updated device {d.Key}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return true;
        }   
    
        /* Deploy specified Manifest to evices satisfying the tagFilter, with module replica count and desired properties */
        public async Task<Boolean> DeployUsingTagsModuleIterator(string tagFilter, string modulesTemplate, string iterateModule, int nIterationCount) {

            IDictionary<string,Twin> devices;
            try {
                tagFilter = tagFilter ?? throw new ArgumentNullException(nameof(tagFilter));
                devices = await GetDevicesByTagFilter(tagFilter);
                //concurrency???
                foreach (var d in devices) {
                    Boolean b = await DeployUsingModuleIterator(d.Key,modulesTemplate, iterateModule,nIterationCount);
                    if (b){
                        Console.WriteLine($"Updated device {d.Key}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return true;
        }


    }
}