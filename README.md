 # Braver

 Braver is an open source reimplementation of the original FF7 game engine.
 More information is [available at the main website](https://braver.ficedula.co.uk/), or there is a Braver channel on the Qhimm Discord for discussion and questions. You can join the Qhimm Discord by following the link [from the forums](https://forums.qhimm.com/)

# Getting Started

After downloading the latest release, run BraverLauncher.exe; you can configure the necessary paths (e.g. where FF7 is installed) here and then click Launch Braver to run the game.

# Plugins

Braver comes with one plugin by default, which adds Tolk (text to speech) support to the game. Plugins are disabled by default so you will need to enable this plugin from within the configuration in BraverLauncher before it will activate.

Once activated, the name of a screen is announced (e.g. which field location has loaded) when the game changes to a new screen; in a menu, the current menu item (and how many options there are) is announced; and when dialog is triggered, the dialog and any choices are announced.

The Tolk plugin also has options to enable footstep sounds which wil play when the controllable character is moving, and a focus sound. When enabled, in the field screens you can cycle through focusable objects using the shoulder buttons (L1/R1 on the controller; the left/right square bracket keys on the keyboard). When a focusable object is selected, the name of the object is announced, and a tone plays every few seconds, higher pitched the closer you get to the object.

Note that because the game wasn't designed for this, the names of the objects aren't necessarily very friendly, and it's possible to focus on an object that isn't actually reachable. Hopefully support for this will improve in future versions!

The battle engine does not have any plugin support yet, so no Tolk output happens here yet. This will improve in a future version as well.
