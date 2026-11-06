


# Mirror-Server-Authoritative-FPS-Controller
Open-source server-authoritative CharacterController for Unity Mirror Networking.

Features:
- ✅ Server authoritative movement
- ✅ Client prediction
- ✅ High ping support (tested up to 200 ms)
- ✅ CharacterController based
- ✅ Anti-cheat friendly
- ✅ Easy integration


# Supported Networking Solutions

- [Mirror](https://github.com/MirrorNetworking/Mirror)


# Info



# How To Integrate

1 [Download](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/archive/refs/heads/main.zip) as a ZIP file.
2 Unpack the file and move it into the Unity editor.
3 Your project must include [Mirror](https://github.com/MirrorNetworking/Mirror).
4 Add the **Character Controller** component to your player prefab.
5 Add the [Mirror FPS Controller.cs](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/blob/main/Scripts/Mirror%20FPS%20Controller.cs) component to your player prefab.
6 Add your camera's rotation object to the "Rot Main Camera" field.
- example:

https://github.com/user-attachments/assets/d5dc8ba1-9b51-4422-b4bc-3325f917ca59

# Customization & Extending Logic

- Basic knowledge of C# is required for the successful integration of the script into the project.

* **`CenMove()`** – Return `false` to block player movement (e.g., when the inventory, chat, or pause menu is open).
* **`CenRotate()`** – Return `false` to block camera rotation (e.g., during shop interactions or cutscenes).
* **`GravityCanBeCalculated(bool isServer)`** – Return `false` to pause gravity logic. Useful for flight modes, swimming, or admin NoClip mechanics.

To integrate these conditions, create a custom script that inherits from [`Mirror FPS Controller.cs`](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/blob/main/Scripts/Mirror%20FPS%20Controller.cs):
```csharp
using UnityEngine;

public class PlayerController : MirrorFPSController
{
    public bool fly = true;
    public bool isInventoryOpen = true;
    public bool stunned = true;

    protected override bool CanMove()
    {
        // Player cannot move if inventory is open
        if(isInventoryOpen) return false;

        return true;
    }

    protected override bool CanRotate()
    {
        //The player cannot rotate the camera while stunned
        if (stunned) return false;

        return true;
    }

    protected override bool GravityCanBeCalculated(bool isServer = false)
    {
        //Gravity is disabled for the player if flight mode is enabled
        if(fly) return false;

        return true;
    }
}
```


# How It Works

- This controller fully implements the concept of the Source Player multiplayer controller, which you can read more about [here](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking):.
- https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking

