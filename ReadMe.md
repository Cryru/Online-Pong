# Story

My friend wanted to play Pong but we couldn't find a working version online so I decided to try to create one. I didn't manage to complete it
in the same evening, but 4 hours later (over the course of 3 days) my self-imposed gamejam speedrun was complete.

All commits after the initial commit are done the initial development.

# Requirements

For hardware requirements check www.github.com/Cryru/Emotion

Compilation requires the .NET 5 SDK

# How to play

Compile a "server" and "client" version of the executable. Solution configurations are prepared for both.
You might want to use a self-contained published .net executable to ensure compatibility.

Include a "ip.txt" file in the same folder as the ".exe" containing nothing but your ip address. Ensure there's no BOM.
Distribute the "client" exe to whomever you want to play with. Launch the server exe and wait for the other player to launch the client.
If you're hosting you might need to port forward the game's port on your router. The default port is 9090 and the configuration is a variable in "Program.cs".

Move the paddles with W/S and play. There is no score limit.

# Features

- Can easily be modified to play another game.
- TCP multiplayer with a custom message protocol.
- Object interpolation between network updates.
- Server authoritative multiplayer model.
