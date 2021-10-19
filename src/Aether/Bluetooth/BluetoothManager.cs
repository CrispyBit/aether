using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using bluez.DBus;
using Tmds.DBus;

namespace Aether.Bluetooth
{
    internal class BluetoothManager
    {
        private readonly string _serviceName = "org.bluez";
        private readonly string _busBluez = "/org/bluez";
        private readonly string _busHci0 = "/org/bluez/hci0";

        private readonly IObjectManager _objectManager;
        private readonly IAgentManager1 _agentManager;
        private readonly IProfileManager1 _profileManager;

        private readonly IAdapter1 _adapter;
        private readonly IGattManager1 _gattManager;
        private readonly ILEAdvertisingManager1 _leAdvertisingManager;
        public BluetoothManager()
        {
            _objectManager = Connection.System.CreateProxy<IObjectManager>(_serviceName, ObjectPath.Root);
            _agentManager = Connection.System.CreateProxy<IAgentManager1>(_serviceName, _busBluez);
            _profileManager = Connection.System.CreateProxy<IProfileManager1>(_serviceName, _busBluez);

            _adapter = Connection.System.CreateProxy<IAdapter1>(_serviceName, _busHci0);
            _gattManager = Connection.System.CreateProxy<IGattManager1>(_serviceName, _busHci0);
            _leAdvertisingManager = Connection.System.CreateProxy<ILEAdvertisingManager1>(_serviceName, _busHci0);

            _objectManager.WatchInterfacesAddedAsync((ifce) =>
            {
                foreach (var foo in ifce.interfaces)
                {
                    Console.WriteLine($"Added Interface:\nKey: {foo.Key}\nValue: {foo.Value}\n\n");
                    foreach(var bacon in foo.Value)
                    {
                        Console.WriteLine($"Added Value Dict Entry:\nKey: {bacon.Key}\nValue: {bacon.Value}\n\n");
                    }
                }
            });

            _objectManager.WatchInterfacesRemovedAsync((ifce) =>
            {
                foreach (var foo in ifce.interfaces)
                {
                    Console.WriteLine($"Removed Interface:{foo}\n\n");
                }
            });
        }

        public async Task Initialize()
        {
            // Prepare advertising configuration
            await _adapter.SetAliasAsync("Aether");
            var props = await _leAdvertisingManager.GetAllAsync();


            IGattCharacteristic1 characteristic = Connection.System.CreateProxy<IGattCharacteristic1>(_serviceName, null);
            characteristic
            // Make aether device discoverable
            await _adapter.SetPairableAsync(true);
            await _adapter.SetDiscoverableAsync(true);
            
        }

        public async Task<string> GetAlias()
        {
            return await _adapter.GetAliasAsync();
        }

        public async Task Cleanup()
        {
            await _adapter.SetDiscoverableAsync(false);
        }

    }
}
