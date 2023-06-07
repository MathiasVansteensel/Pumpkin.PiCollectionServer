using Microsoft.AspNetCore.Http;
using Pumpkin.Networking;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Permissions;
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
    const string ConnectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=Tmp\\base.accdb;Persist Security Info=False;";

	//simulates receiving a message with an httplistener or webhost (like iis or kestrals)
	public static async void ReceiveMessage(byte[] jsonBuffer)
    {
        using (MemoryStream ms = new(jsonBuffer))
        {
            List<PumpkinMessage> messages = await JsonSerializer.DeserializeAsync<List<PumpkinMessage>>(ms);
            WriteToDb(messages);
        }
    }

	static bool red = false, green = false, blue = false;
	static dynamic redVal = 0, greenVal = 0, blueVal = 0;

	private static void WriteToDb(List<PumpkinMessage> msg) 
    {
        for (int i = 0; i < msg.Count; i++)
        {
            PumpkinMessage currentMessage = msg[i];
            string values = string.Empty, fields = string.Empty;
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
						continue;
                    case PumpkinMessage.IDVarLetter.Voltage:
						continue;
					case PumpkinMessage.IDVarLetter.Power:
						continue;
					case PumpkinMessage.IDVarLetter.Power_Factor:
						dbValField = "Powerfactor";
						dbDateField = "PowerfactorTime";
						break;
                    case PumpkinMessage.IDVarLetter.Red:
                        red = true;
                        redVal = kvp.Value;
                        continue;
                    case PumpkinMessage.IDVarLetter.Green:
                        green = true;
                        greenVal = kvp.Value;
                        continue;
                    case PumpkinMessage.IDVarLetter.Blue:
                        blue = true;
                        blueVal = kvp.Value;
                        continue;
                    case PumpkinMessage.IDVarLetter.Lamp_State:
                        LastLampState = ((JsonElement)kvp.Value).ValueKind.ToString() == bool.TrueString ? "ON" : "OFF";
						dbValField = "LampState";
						dbDateField = "LampStateTime";
						break;
                    case PumpkinMessage.IDVarLetter.Lamp_Cycle:
                        LastLampCycleState = ((JsonElement)kvp.Value).ValueKind.ToString() == bool.TrueString ? "ON" : "OFF";
						dbValField = "LampCycle";
						dbDateField = "LampCycleTime";
						break;
                    default:
                        break;
                }
                if (red && green && blue) 
                {
                    red = green = blue = false;
                    LastLampColor = $"RGB({redVal}, {greenVal}, {blueVal})";
                    dbValField = "LampColor";
                    dbDateField = "LampColorTime";
                }

                int length = currentMessage.Content.Count;
                PumpkinMessage.IDVarLetter var = currentMessage.Content.ElementAt((j + 1) % length).Key;
                dynamic val;
				string seperator = (j < length - 1 && (var != PumpkinMessage.IDVarLetter.Red && var != PumpkinMessage.IDVarLetter.Green && var != PumpkinMessage.IDVarLetter.Blue) ? ", " : string.Empty);
                if (var == PumpkinMessage.IDVarLetter.Lamp_State || var == PumpkinMessage.IDVarLetter.Lamp_Cycle) val = ((JsonElement)kvp.Value).ValueKind.ToString() == bool.TrueString ? -1 : 0;
                else val = kvp.Value;
				values += $"'{val}', '{currentMessage.Timestamp}'{seperator}";
				fields += $"{dbValField}, {dbDateField}{seperator}";
			}

            string query = string.Format(AddQuery, fields, values);

            try
            {
				OleDbConnection connection = new(ConnectionString);
				OleDbCommand command = new(query, connection);
				connection.Open();
				command.ExecuteNonQuery();
				connection.Close();
			}
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
		}
	}


}
