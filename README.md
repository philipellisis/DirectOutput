# DirectOutput Framework R3++, Grand Unified Edition

DirectOutput is an add-in for Visual Pinball and other programs that
provides software control over external feedback devices in a virtual
pinball cabinet.  

Feedback devices are things like lights, beacons, solenoids, shaker
motors, and gear motors that augment the "video game" action with
audio, visual, and tactile effects.  These feedback devices are
physically connected to the PC through an "output controller",
typically a USB device.  A variety of output controllers are in common
use, including LedWiz, PacLed, SainSmart USB relay boards, and
open-source systems such as Pinscape.  

DOF acts as a hardware virtualization layer: it provides a common
interface to the different hardware devices so that the pinball
simulator software doesn't have to speak 10 different USB protocols.
DOF also handles all details of effects timing and device state
management, so that the pinball simulator doesn't have to know
anything about the physical devices; it merely sends DOF data on the
abstract game events, and DOF takes care of mapping the game events to
device effects, mapping the device effects to hardware states that
evolve over time, and mapping the hardware states to the output
controller protocol commands necessary to effect same.

DOF documentation can be found at http://directoutput.github.io/DirectOutput/

This is the mjr "Grand Unified" edition of DOF, which merges all of
the known forks as of January 2018.  Several forks with different
add-on features have emerged since the last official DOF release from
SwissLizard in December 2015.  This edition is an attempt to re-unify
all of this work under a single version so that users don't have to
pick subsets of available features - just pick this version and you'll
have them all.

## Build environment setup

The build is set up for Visual Studio 2022.  Load the solution (.sln)
file, and it should take care of the build dependency ordering.

If you want to be able to build the MSI installer, a free third-party
tool called WiX Toolset is required, and must be installed manually.
The MSI installer is the last build step, so if you don't want to
bother with WiX, you can still build everything else; just ignore the
errors on the last step where VS tries to build the MSI.

The WiX toolset requires two separate install steps:

1\. Install the WiX command-line tools from the github release page:
[https://github.com/wixtoolset/wix3/releases](https://github.com/wixtoolset/wix3/releases).
Grab the .EXE for version **3.14.0**, which is a self-installer; run it and you'll be set.

**Important:** As of this writing, you **MUST** use version **3.14.0**.  I can't
tell you how much I hate it when open-source projects tell you that you have to
install a specific out-of-date version of Python or whatever; it always makes me think the
developers have no idea how their build process is supposed to work so they just
insist that you perfectly replicate the house of cards they've constructed out
of old versions of random tools.  So let me explain why this is different.  The WiX
developers discontinued v3 work at 3.14.1 - that's the last release there will
ever be for v3, apparently.  But 3.14.1 is not usable.  It has a known bug that 
makes it generate non-working installs, which fail in a spectacularly annoying 
way, hosing your registry so that you have to clean up the detritus
by hand.  Avoid 3.14.1 at all costs.  So 3.14.0 is the last working v3 release.
To make matters worse, the WiX developers also advise against using any version *earlier*
 than 3.14.1 because of a known security vulnerability, but after studying 
the release notes for 3.14.1, I'm reasonably sure that the vulnerability doesn't 
affect the DOF MSI build (you seem to have to explicitly invoke certain commands
to trigger it, which the DOF MSI doesn't do), so I consider 3.14.0 safe for our use.
And anyway, 3.14.1 simply doesn't work, so we don't have much of a choice.  The
WiX developers know that 3.14.1 is unusable, so when they say you shouldn't use
any earlier version either, what they really mean is that you should migrate to
WiX v4.  I'm not willing to do that at this time because v4 requires a major
rewrite of the install scripts, and it's essentially undocumented.  I investigated
it and decided migrating would simply require more work than I'm willing to 
expend on it right now.  Any volunteers?

2\. Install the WiX add-in tools for Visual Studio. This is done within
Visual Studio: on the main menu, select Extensions > Manage Extensions to bring
up the Extension Manager window.  Select the Browse tab in the window.  
Type **Wix v3** into the search box.  Find **Wix v3 - Visual Studio 2022 Extension**
in the result list.  Click the Install button.

