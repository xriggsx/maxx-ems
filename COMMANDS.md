# EmsMod — Commands & Controls

Open the RPH console with **F4**, type a command, press Enter.

## Go On Duty / Player Kit
| Command | What it does |
|---|---|
| `emsmod_duty` | Go on duty as **Paramedic** (nearest hospital + uniform + ambulance + partner + tools) |
| `emsmod_duty_fire` | Go on duty as **Firefighter** (fire truck + fireman uniform + partner + extinguisher) |
| `emsmod_offduty` | Go off duty (despawns your duty vehicle) |
| `emsmod_duty_char` | Cycle to the next character/uniform for the current mode |
| `emsmod_duty_veh` | Cycle to the next vehicle for the current mode |
| `emsmod_wardrobe` | Open/close the wardrobe (browse clothes + hat/glasses live; EUP uniforms show here) |
| `emsmod_reloadduty` | Reload `Duty.xml` (new uniforms/vehicles/equipment appear without a restart) |

## In-Game Controls (no console needed)
| Control | Keyboard | Xbox controller |
|---|---|---|
| Open/close Duty menu | `F7` | **View** button |
| Menu: move | Arrow **Up/Down** | **D-pad Up/Down** |
| Menu: change value | Arrow **Left/Right** | **D-pad Left/Right** |
| Menu: select | **Enter** | **A** |
| Menu: back/close | **Backspace** | **B** |
| Accept a callout | **Y** | **A** |
| Decline a callout | **N** | **B** |
| Talk / help the patient | **E** | **X** |

## Paramedic Callouts
| Command | Callout |
|---|---|
| `emsmod_callout_caraccident` | Car accident |
| `emsmod_callout_bikeaccident` | Bike accident |
| `emsmod_callout_playgroundfall` | Fall at the park |
| `emsmod_callout_sportsinjury` | Sports injury |
| `emsmod_callout_asthma` | Breathing trouble (asthma) |

## Firefighter Callouts
| Command | Callout |
|---|---|
| `emsmod_callout_housefire` | House fire — smoke inhalation |
| `emsmod_callout_vehiclefire` | Vehicle fire — rescue |
| `emsmod_callout_kitchenfire` | Small fire — burn |

Each callout randomly picks one of 3 injuries (Minor/Moderate/Severe): the patient
pose matches (stand / kneel / lie down), and a Severe one is transported by an
NPC ambulance.

## Config Reload
| Command | What it does |
|---|---|
| `emsmod_reloadconfig` | Reload `General.xml` |
| `emsmod_reloadduty` | Reload `Duty.xml` |
| (callout XML) | Re-read automatically each time you start that callout — just edit the file |

## Debug / Test (optional)
| Command | What it does |
|---|---|
| `emsmod_test_log` | Write one line at each log level |
| `emsmod_test_safe` | Prove error handling catches an exception |
| `emsmod_test_spawn` / `emsmod_test_cleanup` | Spawn / despawn a test ped |
| `emsmod_debug_countspawned` | Report how many spawned entities are still tracked (should be 0 when idle) |
| `emsmod_test_input_start` / `emsmod_test_input_stop` | Log any bound key/button press |
| `emsmod_test_prompt` | Show the accept/decline popup |
| `emsmod_test_voice` | Speak a test dispatch line (TTS) |
| `emsmod_test_callout_start` | Run the content-free TestCallout |
