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
            for (int j = 0; j < currentMessage.Content.Count; j++)
            {
                var kvp = currentMessage.Content.ElementAt(j);

                string dbValField = null, dbDateField = null;

                switch (kvp.Key)
                {
                    case PumpkinMessage.IDVarLetter.Temperature:
                        dbValField = "Temperature";
                        dbDateField = "TemperatureTime";
						break;
                    case PumpkinMessage.IDVarLetter.Humidity:
						dbValField = "Humidity";
						dbDateField = "HumidityTime";
						break;
                    case PumpkinMessage.IDVarLetter.Heat_Index:
						dbValField = "HeatIndex";
						dbDateField = "HeatIndexTime";
						break;
                    case PumpkinMessage.IDVarLetter.Light_Intensity:
						dbValField = "Light";
						dbDateField = "LightTime";
						break;
                    case PumpkinMessage.IDVarLetter.Current:
						dbValField = "Powerfactor";
						dbDateField = "PowerfactorTime";
						break;
                    case PumpkinMessage.IDVarLetter.Voltage:
                        return;
                    case PumpkinMessage.IDVarLetter.Power:
						return;
						break;
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
						dbValField = "Powerfactor";
						dbDateField = "PowerfactorTime";
						break;
                    case PumpkinMessage.IDVarLetter.Lamp_Cycle:
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
            }
        }
    }


}
