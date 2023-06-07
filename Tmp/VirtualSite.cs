using Microsoft.AspNetCore.Http;
using Pumpkin.Networking;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Pumpkin.PiCollectionServer.Tmp;

//simulates the website
internal class VirtualSite
{
    private const string InitText = "No Values Yet!";
    public static dynamic LastTemperature { get; private set; } = InitText;
    public static dynamic LastHeatIndex { get; private set; } = InitText;
	public static dynamic LastHumidity { get; private set; } = InitText;
	public static dynamic LastLampState { get; private set; } = InitText;
	public static dynamic LastLampColor { get; private set; } = InitText;
	public static dynamic LastLampCycleState { get; private set; } = InitText;
	public static dynamic LastLight { get; private set; } = InitText;



	const string AddQuery = "INSERT INTO UserVariables ({0}) VALUES ({1});";

	//simulates receiving a message with an httplistener or webhost (like iis or kestrals)
	public static async void ReceiveMessage(byte[] jsonBuffer)
    {
        using (MemoryStream ms = new(jsonBuffer))
        {
            List<PumpkinMessage> messages = await JsonSerializer.DeserializeAsync<List<PumpkinMessage>>(ms);
            WriteToDb(messages);
        }
    }

    private static void WriteToDb(List<PumpkinMessage> msg) 
    {
        for (int i = 0; i < msg.Count; i++)
        {
            bool red = false, green = false, blue = false;
            PumpkinMessage currentMessage = msg[i];
            string values = string.Empty, dates = string.Empty, valueFields = string.Empty, dateFields = string.Empty;
            for (int j = 0; j < currentMessage.Content.Count; j++)
            {
                var kvp = currentMessage.Content.ElementAt(j);

                string dbValField = null, dbDateField = null;

                switch (kvp.Key)
                {
                    case PumpkinMessage.IDVarLetter.Temperature:
                        LastTemperature = kvp.Value;
                        dbValField = "Temperature";
                        dbDateField = "TemperatureTime";
						break;
                    case PumpkinMessage.IDVarLetter.Humidity:
                        LastHumidity = kvp.Value;
						dbValField = "Humidity";
						dbDateField = "HumidityTime";
						break;
                    case PumpkinMessage.IDVarLetter.Heat_Index:
                        LastHeatIndex = kvp.Value;
						dbValField = "HeatIndex";
						dbDateField = "HeatIndexTime";
						break;
                    case PumpkinMessage.IDVarLetter.Light_Intensity:
                        LastLight = kvp.Value;
						dbValField = "Light";
						dbDateField = "LightTime";
						break;
                    case PumpkinMessage.IDVarLetter.Current:
						return;
                    case PumpkinMessage.IDVarLetter.Voltage:
                        return;
                    case PumpkinMessage.IDVarLetter.Power:
						return;
                    case PumpkinMessage.IDVarLetter.Power_Factor:
						dbValField = "Powerfactor";
						dbDateField = "PowerfactorTime";
						break;
                    case PumpkinMessage.IDVarLetter.Red:
                        red = true;
                        continue;
                    case PumpkinMessage.IDVarLetter.Green:
                        green = true;
                        continue;
                    case PumpkinMessage.IDVarLetter.Blue:
                        blue = true;
                        continue;
                    case PumpkinMessage.IDVarLetter.Lamp_State:
                        LastLampState = kvp.Value;
						dbValField = "Powerfactor";
						dbDateField = "PowerfactorTime";
						break;
                    case PumpkinMessage.IDVarLetter.Lamp_Cycle:
                        LastLampCycleState = kvp.Value;
						dbValField = "Powerfactor";
						dbDateField = "PowerfactorTime";
						break;
                    default:
                        break;
                }
                if (red && green && blue) 
                {
                    red = green = blue = false;
                    dbValField = "LampColor";
                    dbDateField = "LampColorTime";
                }

                //right string += dbfield and val
                //put in query
                //put in db... ez
            }
        }
    }


}
