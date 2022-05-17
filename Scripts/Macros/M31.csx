/**
 * Macro: M31 Tool Offset Measure /w breakage detection
 * UCCNC v1.2115 or higher required with Messages, Probe plugins enabled and fixed probe location set.
 * M31 [Hn] [En.n]
 * - H: Tool offset number (optional, defaults to current tool; if G43Hn is different will fail unless Hn is provided)
 * - E: Tool breakage tolerance value (optional, defaults to no tolerance)
 */

// ### M31 CONFIG ###

#define DEBUG_CSX
#if DEBUG_CSX
public class Macroclass {
  protected UCCNC.Executer exec;
  protected UCCNC.AS3interfaceClass AS3;
  protected UCCNC.Allvarstruct Allvars;

  public void Runmacro() {
#endif

    // should apply G43 tool offset after successful tool probe offset
    var applyToolOffset = true;

    // macros executed before probing tool and after successful probe
    // can be used to retract/remove dust boot and detract/reinstall
    var toolProbePreMacro = "";
    var toolProbePostMacro = "";

    // should prompt before and after for (passive) probe setup, i.e. attach probe lead to tool
    var promptProbeSetup = false;

    // INPUT to detect if a tool is clamped (96=InputPT3PN11)
    var atcLEDToolClamp = 96;
    // INPUT to detect tool release (97=InputPT3PN12)
    var atcLEDToolRelease = 97;

    // Interrupt from failure: 130=Cycle Stop or 512=RESET
    var buttonInterrupt = 130;
    // Cancel from user: 130=Cycle Stop or 512=RESET
    var buttonCancel = 130;

    // safe Z position for travel
    double zSafe = 0.0D;
    // Max Z machine position to probe to
    var probeZ = -5.0D;

    // get retract setting from probe plugin (Retract)
    var probeRetract = double.Parse(AS3.Getfield(2707));
    // get fast feed rate setting from probe plugin (FastFeed)
    var probeFastFeed = double.Parse(AS3.Getfield(2709));
    // get fine feed rate setting from probe plugin (FineFeed)
    var probeFineFeed = double.Parse(AS3.Getfield(2710));

    // get the fixed probe location setting from probe plugin (FixedProbePosX, FixedProbePosY)
    var probePosition = new Position(double.Parse(AS3.Getfield(2726)), double.Parse(AS3.Getfield(2727)));
    // (FixedProbePosZ)
    var probePositionZ = double.Parse(AS3.Getfield(2728));

    // starting numbers to calculate z offset field number
    var fieldStartIdxToolZ_1 = 195; // 196-215 - T1-20
    var fieldStartIdxToolZ_2 = 900; // 921-996 - T21-96

    // ### M31 MACRO ###

    // get the current tool
    var toolCurrentNumber = exec.Getcurrenttool();

    // get the current G43 offset from active modal
    var modalG43 = System.Text.RegularExpressions.Regex.Match(AS3.Getfield(877), "G43H(\\d+)");
    int? offsetCurrentNumber = modalG43.Success ? (int?)int.Parse(modalG43.Groups[1].Value) : null;

    // get the offset tool number
    int? offsetHArgument = Allvars != null ? (Allvars.Hvar != null ? (int?)Allvars.Hvar : null) : null;
    int offsetNumber = offsetHArgument ?? toolCurrentNumber;
    var fieldStartIdxToolZ = offsetNumber < 21 ? fieldStartIdxToolZ_1 : fieldStartIdxToolZ_2;

    // get values needed for breakage detection
    var toolBreakageTolerance = (Allvars != null ? Allvars.Evar : null);
    var offsetCurrentZ = offsetNumber > 0 ? double.Parse(AS3.Getfield(fieldStartIdxToolZ + offsetNumber)) : 0.0D;

    // exec.AddStatusmessage("M31: H" + offsetNumber.ToString() + (toolBreakageTolerance != null ? " E" + toolBreakageTolerance : "") + " (Current Offset: " + offsetCurrentZ + ")");

    if (toolBreakageTolerance != null && toolBreakageTolerance < 0.0D) {
      exec.Callbutton(buttonInterrupt);
      exec.AddStatusmessage("M31: E" + FormatD(toolBreakageTolerance) + " tool tolerance must be absolute value");
      return;
    }

    if (offsetHArgument == null && offsetCurrentNumber != null && offsetCurrentNumber != toolCurrentNumber) {
      exec.Callbutton(buttonInterrupt);
      exec.AddStatusmessage("M31: T" + toolCurrentNumber + " tool number mismatch with G43H" + offsetCurrentNumber);
      exec.AddStatusmessage("M31: Hn must be provided to allow tool number and offset number mismatch");
      return;
    }

    if (offsetNumber < 1 || offsetNumber > 96) {
      exec.Callbutton(buttonInterrupt);
      exec.AddStatusmessage("M31: H" + offsetNumber + " tool offset number out of range");
      return;
    }

    // check if machine is homed/referenced
    if (!exec.GetLED(56) || !exec.GetLED(57) || !exec.GetLED(58)) {
      exec.Callbutton(buttonInterrupt);
      Prompt("M31: Tool Offset Probe", "Machine is not homed", "OK", PromptStatus.Error);
      return;
    }

    // check if fixed probed location is set
    if (!exec.GetLED(307)) {
      exec.Callbutton(buttonInterrupt);
      Prompt("M31: Tool Offset Probe", "Fixed probe location not saved", "OK", PromptStatus.Error);
      return;
    }

    // check that there is a tool in spindle
    if (toolCurrentNumber < 1 || !AssertClampPosition(atcLEDToolClamp, true, atcLEDToolRelease, false)) {
      exec.Callbutton(buttonInterrupt);
      Prompt("M31: Tool Offset Probe", "Tool not in spindle", "OK", PromptStatus.Error);
      return;
    }

    var toolMasterNumber = 0;
    // find master tool number
    for (var i = 1; i <= 96; i++) {
      var toolTypeFlags = exec.Readkey("Tooltablevalues", "Type" + i, "").Split(new char[] { ' ', ',', ';' });
      if (Array.Exists(toolTypeFlags, f => string.Equals(f, "Master", StringComparison.InvariantCultureIgnoreCase))) {
        toolMasterNumber = i;
        break;
      }
    }
    if (toolMasterNumber < 1) {
      exec.Callbutton(buttonInterrupt);
      Prompt("M31: Tool Offset Probe", "No master tool reference found", "OK", PromptStatus.Error);
      return;
    }

    // get master tool description
    var toolMasterDesc = exec.Readkey("Tooltablevalues", "Description" + toolMasterNumber, "");

    // get the Z machine position of the master tool, fail if one is not set
    var toolMasterProbeZ = double.Parse(exec.Readkey("M31Macro", "MasterToolProbeZ", "NaN"));
    if (double.NaN.Equals(toolMasterProbeZ)) {
      if (offsetNumber != toolMasterNumber) {
        exec.Callbutton(buttonInterrupt);
        Prompt("M31: Tool Offset Probe", "T" + toolMasterNumber + " master tool reference not probed", "OK", PromptStatus.Error);
        return;
      }
      toolMasterProbeZ = 0.0D;
    }

    // if the tool offset is master, warn of updating master
    if (offsetNumber == toolMasterNumber) {
      var result = Prompt("M31: Tool Offset Probe", "Probing T" + toolMasterNumber + " master tool reference may skew current known tool offsets\nContinue probing master tool reference?\n" + toolMasterDesc, "OKCancel", PromptStatus.Warning);

      if (result != DialogResult.OK) {
        exec.AddStatusmessage("M31: Tool offset probe was canceled");
        exec.Callbutton(buttonCancel);
        return;
      }
    }

    while (exec.IsMoving()) { };
    // store original position to move back to
    var originalPosition = new Position(exec.GetXmachpos(), exec.GetYmachpos());
    var originalModal = AS3.Getfield(877).Split('|');

    if (!ExecuteGCode(
        // execute any pre-macro
        toolProbePreMacro,
        // stop coolant and spindle
        "M9", "M5",
        // move to safe z
        "G90 G00 G53 Z" + zSafe,
        // move to fixed probe
        "G00 G53 X" + probePosition.X + " Y" + probePosition.Y,
        "G00 G53 Z" + probePositionZ,
        // cancel out tool offset, scale and rotation
        "G49 G50 G69"
    )) {
      exec.AddStatusmessage("M31: tool offset probe interrupted");
      return;
    }

    if (promptProbeSetup) {
      var result = Prompt("M31: Tool Offset Probe", "Setup tool probe and press OK to continue", "OK", PromptStatus.Warning);
      if (result != DialogResult.OK) {
        exec.AddStatusmessage("M31: tool offset probe interrupted");
        return;
      }
    }

    // two stage probe, fast then fine feed rate
    for (var i = 0; i < 2; i++) {
      // first probe use fast feed, second use fine feed
      var feedRate = i < 1 ? probeFastFeed : probeFineFeed;
      // first probe use fixed probe distance, second use retract distance
      var z = i < 1 ? probeZ : -probeRetract;

      if (!ExecuteGCode(
          // start probing
          "G91 F" + feedRate + " G31 Z" + z
      )) {
        exec.AddStatusmessage("M31: tool offset probe interrupted");
        return;
      }

      // check probe status outcome
      var probeStatus = exec.Getvar(5060);
      if (probeStatus == 1) {
        exec.AddStatusmessage("M31: Failed to probe H" + offsetNumber + " tool offset with-in travel");
        exec.Callbutton(buttonInterrupt);
        return;
      } else if (probeStatus != 0) {
        exec.AddStatusmessage("M31: Failed to probe H" + offsetNumber + " tool offset (ERR" + probeStatus + ")");
        exec.Callbutton(buttonInterrupt);
        return;
      }

      if (!ExecuteGCode(
          // retract
          "G91 G00 Z" + probeRetract
      )) {
        exec.AddStatusmessage("M31: tool offset probe interrupted");
        return;
      }
    }

    if (promptProbeSetup) {
      var result = Prompt("M31: Tool Offset Probe", "Remove tool probe and press OK to continue", "OK", PromptStatus.Warning);
      if (result != DialogResult.OK) {
        exec.AddStatusmessage("M31: tool offset probe interrupted");
        return;
      }
    }

    if (!ExecuteGCode(
            // change to absolute mode
            "G90",
            // Go to safe Z
            "G00 G53 Z" + zSafe
    )) {
      exec.AddStatusmessage("M31: tool offset probe interrupted");
      return;
    }

    if (!exec.Ismacrostopped()) {
      // get probe Z result
      var offsetProbeZ = exec.Getvar(5063);
      // add current work offset to get probe in machine pos
      offsetProbeZ += exec.GetZmachpos() - exec.GetZpos();

      // calculate offset Z from master tool (zero out if is master tool)
      var offsetToolZ = offsetNumber != toolMasterNumber ? offsetProbeZ - toolMasterProbeZ : 0.0D;

      if (toolBreakageTolerance != null) {
        // check for tool is with-in tollerance
        var offsetDifference = Math.Abs(offsetCurrentZ - offsetToolZ);
        if (offsetDifference > toolBreakageTolerance) {
          exec.AddStatusmessage("M31: Tool offset H" + offsetNumber + " measured Z" + FormatD(offsetToolZ) + "; currently Z" + FormatD(offsetCurrentZ));
          exec.AddStatusmessage("M31: Tool offset H" + offsetNumber + " measurement is out of tolerance: " + FormatD(offsetDifference) + ">" + FormatD(toolBreakageTolerance));
          exec.Callbutton(buttonInterrupt);
          Prompt("M31: Tool Offset Probe", "Tool breakage detected for T" + toolCurrentNumber + " H" + offsetNumber, "OK", PromptStatus.Error);
          return;
        } else {
          exec.AddStatusmessage("M31: Tool offset H" + offsetNumber + " measured Z" + FormatD(offsetToolZ) + "; currently Z" + FormatD(offsetCurrentZ));
          exec.AddStatusmessage("M31: Tool offset H" + offsetNumber + " measurement is with-in tolerance: " + FormatD(offsetDifference) + "<=" + FormatD(toolBreakageTolerance));
        }
      }

      // update tool master
      if (offsetNumber == toolMasterNumber) {
        var masterZTo = FormatD(offsetProbeZ);
        var masterZFrom = FormatD(toolMasterProbeZ);

        exec.AddStatusmessage("M31: Setting T" + toolMasterNumber + " master tool reference" + (masterZTo != masterZFrom ? " from Z" + masterZFrom : "") + " to Z" + masterZTo);
        exec.Writekey("M31Macro", "MasterToolProbeZ", masterZTo);
      }

      var offsetTo = FormatD(offsetToolZ);
      var offsetFrom = FormatD(offsetCurrentZ);
      exec.AddStatusmessage("M31: Setting H" + offsetNumber + " tool offset" + (offsetTo != offsetFrom ? " from Z" + offsetFrom : "") + " to Z" + offsetTo);

      // update tool offset in tool table field
      AS3.Setfieldtext(offsetToolZ.ToString(), fieldStartIdxToolZ + offsetNumber);
      AS3.Validatefield(fieldStartIdxToolZ + offsetNumber);

      // save tool offset in profile
      exec.Writekey("Tooltablevalues", "TooloffsetZ" + offsetNumber, offsetToolZ.ToString());

      if (!ExecuteGCode(
              // Go to safe Z
              "G00 G53 Z" + zSafe
      )) {
        exec.AddStatusmessage("M31: tool offset probe interrupted");
        return;
      }

      if (!ExecuteGCode(
          // execute any post-macro
          toolProbePostMacro,
          // move to safe z
          "G00 G53 Z" + zSafe,
          // apply tool offset if enabled
          (applyToolOffset ? "G43 H" + offsetNumber : ""),
          // restore modal
          String.Join(" ", Array.FindAll(originalModal,
              // if applyToolOffset, filter out original G43/G49
              modal => !applyToolOffset || (!modal.StartsWith("G43") && modal != "G49")
          ))
      )) {
        exec.AddStatusmessage("M31: tool offset probe interrupted");
        return;
      }
    } else {
      exec.AddStatusmessage("M31: tool offset probe was interrupted");
      exec.Callbutton(buttonInterrupt);
      return;
    }

#if DEBUG_CSX
  }
#endif
  //#Events
  // ### GLOBAL UTILS ###

  private bool ExecuteGCode(params string[] lines) {
    if (exec.Ismacrostopped() || exec.GetLED(25)) {
      return false;
    }

    var gcode = new List<string>(lines);
    // gcode.ForEach(line =>  exec.AddStatusmessage(line));
    exec.Codelist(gcode);
    while (exec.IsMoving()) { }

    var result = !exec.Ismacrostopped() && !exec.GetLED(25); // !STOP && !RESET
    return result;
  }

  private string FormatD(double? num) {
    return num != null ? String.Format("{0:0.0###}", num.Value) : "<null>";
  }

  private DialogResult Prompt(string title, string messsage, string button, PromptStatus status = PromptStatus.None) {
    var result = exec.Informplugin("Messages.dll",
    string.Format("{0}{1}:{2}|{3}",
        status == PromptStatus.Error ? "#" : status == PromptStatus.Warning ? "!" : "",
        button, title, messsage
    ));
    return result is DialogResult ? (DialogResult)result : DialogResult.None;
  }

  private bool AssertClampPosition(int clampLed, bool clampExpected, int releaseLed, bool? releaseExpected = null) {
    var retry = 4;
    var debounce = 2;
    var result = false;

    do {
      // dwell after first
      if (debounce < 2 || retry < 4) { Thread.Sleep(250); }

      var clampState = exec.GetLED(clampLed);
      var releaseState = exec.GetLED(releaseLed);

      result = clampState == clampExpected && (releaseExpected == null || releaseState == releaseExpected.Value);
      debounce = result ? debounce - 1 : 2;
      retry = result ? retry : retry - 1;
    } while (retry > 0 && debounce > 0 && !result);

    return result;
  }

  private enum PromptStatus {
    Error = -1,
    None = 0,
    Warning = 1
  }

  private struct Position {
    public Position(double x, double y) {
      this.X = x;
      this.Y = y;
    }

    public double X;
    public double Y;
  }

  private struct PortPin {
    public PortPin(int port, int pin) {
      this.Port = port;
      this.Pin = pin;
    }

    public int Port;
    public int Pin;
  }

#if DEBUG_CSX
}
#endif
