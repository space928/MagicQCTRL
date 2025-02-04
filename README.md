# MagicQCTRL
A custom controller for the MagicQ lighting software built around the Adafruit MacroPad RP2040.
![image](https://github.com/space928/MagicQCTRL/assets/15130114/3cb6eed8-b5b3-486b-bec7-f252c55654e3)

## Building
The project is split into two parts, the software which runs on the macropad and the client application.

### Macropad Software
The software running on the MacroPad is a simple Arduino project which can be built using the PlatformIO extension for VSCode.

### Client Application
The client application is written in .NET 7 and can be built with any modern version of Visual Studio with support for .NET 7.

## Data Flow

```


  ==================       USB HID Messages    [________________ - [] X ]                          [_______________ - [] X ]
  | O  #####  O  O |   ----- Key Press ----->  [                        ]  ---- OSC Message ---->  [ MagicQ                ]
  | O  [] [] []  O |   ---- Button Press --->  [                        ]                          [                       ]
  | O  [] [] []  O |   ---- Encoder Move --->  [                        ]  ---- Memory Read ---->  [                       ]
  | O  [] [] []  O |                           [                        ]  <--- Memory Write ----  [                       ]
  |    [] [] []    |   <-- Update Profile ---  [                        ]                          [                       ]
  |                |                           [                        ]                          [                       ]
  ==================                           [________________________]                          [                       ]
  MagicQ CTRL Hardware                         Client app                                          MagicQ


```
