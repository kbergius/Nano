### Note: this repository is under construction. Please, come back after couple of days.


This is a test graphics engine for NanoUI.

It is stripped down & modified version of the [MoonWorks engine](https://github.com/MoonsideGames/MoonWorks).
See licences in **licences** folder.

Main modifications:
- removed input system (uses "raw" SDL events)
- removed text rendering
- modified Game class

In order to get engine running, you must extract the prebuilt binaries from the **binaries** folder and
copy them to the location where your application's executable is (use only binaries that suit your OS).

Note: **Wellspring.XXX** library (text rendering) is not used.

Note2: Image loading/saving (**IRO**) is configured to support only PNG images.

### Credits
- [MoonWorks engine](https://github.com/MoonsideGames/MoonWorks)
