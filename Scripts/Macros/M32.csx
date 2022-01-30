/**
 * Macro: M32 Probe Routine
 * Execute a Subroutine G-Code program
 * M32 { Pn ... }
 * - P: Probe routine to execute
 */

#define DEBUG_CSX
#if DEBUG_CSX
public class Macroclass {
  protected UCCNC.Executer exec;
  protected UCCNC.AS3interfaceClass AS3;
  protected UCCNC.Allvarstruct Allvars;

  public void Runmacro() {
#endif
    // ### M32 CONFIG ###

    // get retract setting from probe plugin
    var probeRetract = double.Parse(AS3.Getfield(2707)); // Retract

    // get fast feed rate setting from probe plugin
    var probeFastFeed = double.Parse(AS3.Getfield(2709)); // FastFeed

    // get fine feed rate setting from probe plugin
    var probeFineFeed = double.Parse(AS3.Getfield(2710)); // FineFeed

    // get limited travel settings from probe plugin or set feedrate
    var probeTravelFeed = AS3.Getbuttonstate(875) ? double.Parse(AS3.Getfield(2737)) : double.Parse(AS3.Getfield(867)); // TraverseSpeedLimit or Setfeedrate

    var toolNumber = exec.Getcurrenttool();
    var fieldStartIdxToolDia = 2500; // 2501-2596
    var probeDia = toolNumber > 0 ? double.Parse(AS3.Getfield(fieldStartIdxToolDia + toolNumber)) : 0D;

    // ### M32 MACRO ### 

    var pVar = Allvars != null ? (double?)Allvars.Pvar : null;
    if (!Validate(pVar != null, "M32: P is required")) {
      return;
    }
    if (!Validate(pVar.Value % 1 == 0, "M32: P must be an integer")) {
      return;
    }
    var pRoutine = (ProbeRoutine)(int)pVar.Value;
    if (!Validate(ProbeRoutines.ContainsKey(pRoutine), string.Format("M32: P{0} probe routine not found", (int)pRoutine))) {
      return;
    }

    var probeSettings = new ProbeSettings(probeDia, probeRetract, probeFastFeed, probeFineFeed, probeTravelFeed);

    var routine = ProbeRoutines[pRoutine];
    if (!routine.Execute(this, probeSettings, Allvars)) {
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
    gcode.ForEach(line => exec.AddStatusmessage(line));
    exec.Codelist(gcode);
    while (exec.IsMoving()) { }

    var result = !exec.Ismacrostopped() && !exec.GetLED(25); // !STOP && !RESET
    return result;
  }

  public void ClipboardSetText(string text) {
    Thread thread = new Thread(() => System.Windows.Forms.Clipboard.SetText(text));
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
  }

  public string FormatD(double? num) {
    return num != null ? String.Format("{0:0.0###}", num.Value) : "<null>";
  }

  public bool Validate(bool validation, string message) {
    if (!validation) {
      exec.AddStatusmessage(message);
    }
    return validation;
  }

  public double GetWCSPosition(Axis axis) {
    return axis == Axis.X ? exec.GetXpos() : axis == Axis.Y ? exec.GetYpos() : exec.GetZpos();
  }

  public double GetWCSScale(Axis axis) {
    return axis == Axis.X ? exec.GetXscale() : axis == Axis.Y ? exec.GetYscale() : exec.GetZscale();
  }

  public double GetMachinePosition(Axis axis) {
    return axis == Axis.X ? exec.GetXmachpos() : axis == Axis.Y ? exec.GetYmachpos() : exec.GetZmachpos();
  }

  public double Distance(double a, double b) {
    var distance = a > b ? a - b : b - a;
    return Math.Abs(distance);
  }

  public double Distance(double x1, double y1, double x2, double y2) {
    var distance = Math.Sqrt(Math.Pow((x2 - x1), 2) + Math.Pow((y2 - y1), 2));
    return distance;
  }

  public bool Intersection(
    double aX1, double aY1, double aX2, double aY2,
    double bX1, double bY1, double bX2, double bY2,
    out double x, out double y
  ) {
    double a1 = aY2 - aY1;
    double b1 = aX1 - aX2;
    double c1 = a1 * aX1 + b1 * aY1;

    double a2 = bY2 - bY1;
    double b2 = bX1 - bX2;
    double c2 = a2 * bX1 + b2 * bY1;

    double delta = a1 * b2 - a2 * b1;

    if (delta == 0D) {
      x = 0D;
      y = 0D;
      return false;
    }

    x = (b2 * c1 - b1 * c2) / delta;
    y = (a1 * c2 - a2 * c1) / delta;
    return true;
  }

  public void Perpendicular(
    double aX1, double aY1, double aX2, double aY2,
    double bX, double bY,
    out double x, out double y
  ) {
    double a1 = aY2 - aY1;
    double b1 = aX1 - aX2;
    double c1 = a1 * aX1 + b1 * aY1;

    double k = ((aY2 - aY1) * (bX - aX1) - (aX2 - aX1) * (bY - aY1)) / (Math.Pow(aY2 - aY1, 2) + Math.Pow(aX2 - aX1, 2));
    x = bX - k * (aY2 - aY1);
    y = bY + k * (aX2 - aX1);
  }

  public double RadiansToDegrees(double radians) {
    return radians * (180D / Math.PI);
  }

  public double AngleRadians(double x1, double y1, double x2, double y2) {
    double angle = Math.Atan2(y1 - y2, x1 - x2);
     // if obtuse, get acute angle
    // var piHalf = (Math.PI * 0.5D);
    // angle = angle > piHalf ? angle = Math.PI - angle : angle;
    return angle;
  }

  public double AngleRadians(double a1, double a2) {
    double angle = a2 - a1;
    // if obtuse, get acute angle
    var piHalf = (Math.PI * 0.5D);
    angle = angle > piHalf ? angle = Math.PI - angle : angle;
    return angle;
  }

  public double MaxDifference(params double[] differences) {
    double maxDiff = differences[0];
    foreach (var diff in differences) {
      if (Math.Abs(maxDiff) < Math.Abs(diff)) {
        maxDiff = diff;
      }
    }
    return maxDiff;
  }

  public enum LogLevel {
    Error = -1,
    Info = 0
  }

  public enum ProbeRoutine {
    Surface = 1,
    PocketOrWeb = 2,
    BoreOrBoss = 3,
    BoreOrBoss3Point = 4,
    InternalCorner = 5,
    ExternalCorner = 6,
    SurfaceAngle = 7
  }

  public enum ProbeOutputNum {
    XPosition = 2100,
    YPosition = 2101,
    ZPosition = 2102,

    XDimension = 2104, // width, dia or angle
    YDimension = 2105,
    ZDimension = 2106,

    Dimension = 2109, // max measure for +2 datums

    XPositionError = 2110,
    YPositionError = 2111,
    ZPositionError = 2112,

    XDimensionError = 2114, // width, dia or angle
    YDimensionError = 2115,
    ZDimensionError = 2116,

    TruePositionError = 2117,
    PositionError = 2118, // max error for +2 datums
    DimensionError = 2119,
    OutOfTolerance = 2120, // 0 = in-tolerance, 1 = out of tolerance

    ComponentNumber = 2130,
    FeatureNumber = 2131
  }

  public enum GetVarResult {
    Invalid = -1,
    Missing = 0,
    Successful = 1
  }

  public enum Axis {
    X,
    Y,
    Z
  }

  public struct ProbeSettings {
    public ProbeSettings(double dia, double retract, double fast, double fine, double travel) : this() {
      this.Diameter = dia;
      this.Retract = retract;
      this.FastFeedRate = fast;
      this.FineFeedRate = fine;
      this.TravelFeedRate = travel;
    }

    public double Diameter { get; private set; }
    public double Retract { get; private set; }
    public double FastFeedRate { get; private set; }
    public double FineFeedRate { get; private set; }
    public double TravelFeedRate { get; private set; }
  }

  public struct ProbeInput {
    public ProbeInput(char letter, string name, bool required, ProbeInputGroup group = new ProbeInputGroup()) : this() {
      this.Letter = letter;
      this.Name = name;
      this.IsRequired = required;
      this.IsInteger = false;
      this.MinValue = null;
      this.MaxValue = null;
      this.Group = group;
    }

    public ProbeInput(char letter, string name, bool required, bool integer, int? min, int? max, ProbeInputGroup group = new ProbeInputGroup())
    : this(letter, name, required, group) {
      this.IsInteger = integer;
      this.MinValue = min;
      this.MaxValue = max;
    }

    public char Letter { get; private set; }
    public string Name { get; private set; }
    public bool IsRequired { get; private set; }
    public bool IsInteger { get; private set; }
    public int? MinValue { get; private set; }
    public int? MaxValue { get; private set; }

    public ProbeInputGroup Group { get; private set; }
  }

  public struct ProbeInputGroup {
    public ProbeInputGroup(char[] or = null, char[] requires = null) : this() {
      this.Or = or ?? new char[0];
      this.Requires = requires ?? new char[0];
    }

    private char[] _or;
    private char[] _requires;
    public char[] Or { get { return _or ?? new char[0]; } private set { _or = value ?? new char[0]; } }
    public char[] Requires { get { return _requires ?? new char[0]; } private set { _requires = value ?? new char[0]; } }
  }

  static public Dictionary<ProbeRoutine, ProbeRoutineBase> ProbeRoutines = CreateProbeRoutines();
  static public Dictionary<ProbeRoutine, ProbeRoutineBase> CreateProbeRoutines() {
    var routines = new Dictionary<ProbeRoutine, ProbeRoutineBase>();
    foreach (var routineType in Array.FindAll(typeof(ProbeRoutineBase).Assembly.GetTypes(), t => t.IsClass && !t.IsAbstract && typeof(ProbeRoutineBase).IsAssignableFrom(t))) {
      var routine = (ProbeRoutineBase)Activator.CreateInstance(routineType);
      routines.Add(routine.Routine, routine);
    }
    return routines;
  }

  /**
   * U: Tool wear update tolerance limit
   * V: Tool wear update threshold
   */

  public abstract class ProbeRoutineBase {
    protected ProbeRoutineBase() {
      Inputs = new List<ProbeInput>(new[]{
                new ProbeInput('Q', "Probe overtravel", true, false, 0, null),
                new ProbeInput('W', "WCS index number", false, true, 1, 6),
                new ProbeInput('U', "WCS or tool wear tolerance", false, false, 0, null, new ProbeInputGroup(null, new []{'W', 'H'})),
                new ProbeInput('O', "Report increment feature or component", false, true, 1, 2),
            });
    }

    abstract public ProbeRoutine Routine { get; }
    abstract internal bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs);

    protected List<ProbeInput> Inputs { get; private set; }

    public bool Execute(Macroclass macro, ProbeSettings settings, UCCNC.Allvarstruct allVars) {
      var vars = new Dictionary<char, double?>();
      if (allVars != null) {
        foreach (var prop in allVars.GetType().GetFields()) {
          if (prop.Name.Length != 4 || !prop.Name.EndsWith("var")) { continue; }
          var value = (double?)prop.GetValue(allVars);
          vars.Add(prop.Name[0], value);
        }
      }

      var inputs = new Dictionary<char, double>();
      foreach (var input in this.Inputs) {

        var value = vars.ContainsKey(input.Letter) ? vars[input.Letter] : null;

        if (!macro.Validate(
            !input.IsRequired || value != null || Array.FindIndex(input.Group.Or, l => vars.ContainsKey(l) && vars[l] != null) > -1,
            string.Format("M32: {0}{1} word is required", input.Letter, input.Group.Or.Length > 0 ? " or " + string.Join(" or ", input.Group.Or) : "")
        )) {
          return false;
        }
        if (value == null) { continue; }

        if (!macro.Validate(input.Group.Or.Length < 1 || Array.FindIndex(input.Group.Or, l => vars.ContainsKey(l) && vars[l] != null) < 0,
            string.Format("M32: Only one {0}{1} word allowed", input.Letter, input.Group.Or.Length > 0 ? " or " + string.Join(" or ", input.Group.Or) : "")
        )) {
          return false;
        }
        if (!macro.Validate(input.Group.Requires.Length < 1 || Array.FindIndex(input.Group.Requires, l => vars.ContainsKey(l) && vars[l] != null) > -1,
            string.Format("M32: {0} word requires {1} word", input.Letter, string.Join(" or ", Array.FindAll(input.Group.Requires, l => this.Inputs.FindIndex(i => i.Letter == l) > -1)))
        )) {
          return false;
        }
        if (!macro.Validate(!input.IsInteger || value.Value % 1 == 0, string.Format("M32: {0} must be an integer, {1}", input.Letter, input.Name))) {
          return false;
        }
        if (!macro.Validate(
            (input.MinValue == null || value.Value >= input.MinValue.Value) &&
            (input.MaxValue == null || value.Value <= input.MaxValue.Value),
            string.Format("M32: {0} word is out of range", input.Letter))) {
          return false;
        }

        // macro.exec.AddStatusmessage(input.Letter + ":" + value.Value);

        inputs.Add(input.Letter, value.Value);
      }

      // zero out probe vars
      /*
      // BUG: must be async, as after actual Setvar with value then Getvar ends up returning zero

      // exclude global output nums
      var resetOutputNums = Array.FindAll((ProbeOutputNum[])Enum.GetValues(typeof(ProbeOutputNum), o => o != ProbeOutputNum.ComponentNumber &&
                                                                                                  o != ProbeOutputNum.FeatureNumber);
      foreach(var outputNum in resetOutputNums){
          macro.exec.Setvar(0D, (int)outputNum);
      }
      while(macro.exec.IsMoving()){};
      */

      Dictionary<ProbeOutputNum, double> outputs;
      var result = this.InternalExecute(macro, settings, inputs, out outputs);

      foreach (var output in outputs) {
        macro.exec.Setvar(output.Value, (int)output.Key);
        macro.exec.AddStatusmessage(output.Key.ToString() + " #" + (int)output.Key + ": " + macro.FormatD(output.Value));
      }
      while (macro.exec.IsMoving()) { }

      return result;
    }

    protected bool AssertTolerance(Macroclass macro, string label, double? tolerance, params double?[] errors) {
      if (tolerance != null) {
        foreach (var error in errors) {
          if (error == null) { continue; }
          if (Math.Abs(error.Value) > tolerance) {
            macro.exec.AddStatusmessage("M32: " + label + " error is out of tolerance: " + macro.FormatD(error.Value) + " > ±" + macro.FormatD(tolerance));
            return false;
          }
          else {
            macro.exec.AddStatusmessage("M32: " + label + " error is with-in tolerance: " + macro.FormatD(error.Value) + " <= ±" + macro.FormatD(tolerance));
          }
        }
      }
      return true;
    }

    public void AppendToReport(Macroclass macro, int? reportIncrement, Dictionary<ProbeOutputNum, double> outputs) {
      if (reportIncrement == null) {
        return;
      }

      var componentNumber = (int)macro.exec.Getvar((int)ProbeOutputNum.ComponentNumber);
      var featureNumber = (int)macro.exec.Getvar((int)ProbeOutputNum.FeatureNumber);

      if (reportIncrement.Value == 1) {
        featureNumber += 1;
      }
      else if (reportIncrement.Value == 2) {
        componentNumber += 1;
      }
      else {
        return;
      }

      outputs.Add(ProbeOutputNum.ComponentNumber, componentNumber);
      outputs.Add(ProbeOutputNum.FeatureNumber, featureNumber);

      var gcodeFilepath = macro.AS3.Getfield(895); // loaded gcode file
      try {
        if (
          macro.exec.GetLED(216) || // Is Running MDI
          gcodeFilepath.Length < 1 || gcodeFilepath == "-" ||
          !System.IO.File.Exists(gcodeFilepath)
        ) {
          var reportsPath = System.IO.Path.Combine(System.Environment.CurrentDirectory, "Probe Reports");
          if (!System.IO.Directory.Exists(reportsPath)) {
            System.IO.Directory.CreateDirectory(reportsPath);
          }

          gcodeFilepath = System.IO.Path.Combine(reportsPath, DateTime.Now.ToString("yyyy-MM-dd"));
        }
        var reportFilepath = gcodeFilepath + ".report.csv";

        var outputNums = Array.FindAll((ProbeOutputNum[])Enum.GetValues(typeof(ProbeOutputNum)), o => o != ProbeOutputNum.ComponentNumber &&
                                                                                                    o != ProbeOutputNum.FeatureNumber);
        if (!System.IO.File.Exists(reportFilepath)) {
          var header = new List<string>(new string[]{
            "Date",
            ProbeOutputNum.ComponentNumber.ToString(),
            ProbeOutputNum.FeatureNumber.ToString(),
            typeof(ProbeRoutine).Name
          });
          Array.ForEach(outputNums, outputNum => header.Add(outputNum.ToString()));

          // write csv header
          System.IO.File.AppendAllText(reportFilepath, string.Join(",", header) + System.Environment.NewLine);
        }

        var row = new List<string>(new string[]{
          DateTime.Now.ToString("o"),
          componentNumber.ToString(),
          featureNumber.ToString(),
          "P" + (int)this.Routine
        });
        Array.ForEach(outputNums, outputNum => row.Add(outputs.ContainsKey(outputNum) ? macro.FormatD(outputs[outputNum]) : ""));

        // write report row
        System.IO.File.AppendAllText(reportFilepath, string.Join(",", row) + System.Environment.NewLine);
      }
      catch (Exception ex) {
        macro.exec.AddStatusmessage("M32: Failed to write probe report for component #" + componentNumber + ", feature #" + featureNumber + "; " + ex.Message);
      }
    }

    protected bool UpdateWCS(Macroclass macro, int? wcsIndex, Dictionary<char, double> inputs, params Tuple<double, double>[] positionsWCS) {
      var tolerance = inputs.ContainsKey('U') ? (double?)inputs['U'] : null;

      if (wcsIndex != null) {
        var wcsOffsets = new List<string>();

        for (var i = 0; i < positionsWCS.Length; i++) {
          if (i > 2) { break; }
          if (positionsWCS[i] == null) { continue; }

          var axis = i == 0 ? Axis.X : i == 1 ? Axis.Y : Axis.Z;
          var positionWCS = positionsWCS[i].Item1;
          var positionError = positionsWCS[i].Item2;

          if (tolerance != null && Math.Abs(positionError) > tolerance) {
            macro.exec.AddStatusmessage("M32: WCS " + axis.ToString() + " is out of tolerance: " + macro.FormatD(positionError) + " > ±" + macro.FormatD(tolerance));
          }

          var currentScaleWCS = macro.GetWCSScale(axis);
          var currentPositionWCS = macro.GetWCSPosition(axis);
          var currentPositionMach = macro.GetMachinePosition(axis);

          // calculate position in machine cordinates
          var positionMach = (positionWCS * currentScaleWCS) + (currentPositionMach - (currentPositionWCS * currentScaleWCS));

          wcsOffsets.Add(string.Format("{0}{1}", axis.ToString(), macro.FormatD(positionMach)));
        }

        if (wcsOffsets.Count > 0) {
          macro.exec.AddStatusmessage("M32: Setting G" + (53 + wcsIndex.Value) + " offset to " + string.Join(" ", wcsOffsets));

          // update WCS with position offset
          if (!macro.ExecuteGCode(string.Format("G10 L2 P{0} {1}", wcsIndex.Value, string.Join(" ", wcsOffsets)))) {
            return false;
          }
        }
      }
      return true;
    }

    protected bool UpdateToolWear(Macroclass macro, Axis axis, double correctionAmount, Dictionary<char, double> inputs) {
      if (!inputs.ContainsKey('H')) {
        return true;
      }

      var toolOffsetNumber = (int)inputs['H'];
      var correctionPrecentage = inputs.ContainsKey('K') ? inputs['K'] : 1D;
      var correctionTolerance = inputs.ContainsKey('U') ? (double?)inputs['U'] : null;
      var correctionDeadZone = inputs.ContainsKey('V') ? (double?)inputs['V'] : null;

      // apply correction precentage
      var wearAmount = correctionAmount * correctionPrecentage;

      // check if above dead zone
      if (correctionDeadZone != null && Math.Abs(wearAmount) < correctionDeadZone.Value) {
        macro.exec.AddStatusmessage("M32: T" + toolOffsetNumber + " tool wear correction skipped, is below dead zone: " + axis.ToString() + macro.FormatD(wearAmount) + " < ±" + macro.FormatD(correctionDeadZone));
        return true;
      }

      // check if below tolerance
      if (correctionTolerance != null && Math.Abs(wearAmount) > correctionTolerance.Value) {
        macro.exec.AddStatusmessage("M32: T" + toolOffsetNumber + " tool wear correction is out of tolerance: " + axis.ToString() + macro.FormatD(wearAmount) + " > ±" + macro.FormatD(correctionTolerance));
        return false;
      }

      // determine the tool offset field, either length (Z) or diameter (X/Y)
      var fieldStartIdxToolZ_1 = 195; // 196-215 - T1-20
      var fieldStartIdxToolZ_2 = 900; // 921-996 - T21-96
      var fieldStartIdxToolDia = 2500; // 2501-2596
      var fieldIdxToolAxisOffset = axis == Axis.Z ? (toolOffsetNumber < 21 ? fieldStartIdxToolZ_1 : fieldStartIdxToolZ_2) : fieldStartIdxToolDia;
      fieldIdxToolAxisOffset += toolOffsetNumber;

      var offsetLabel = (axis == Axis.Z ? "Z" : "Dia");
      var toolAxisOffset = double.Parse(macro.AS3.Getfield(fieldIdxToolAxisOffset));

      // apply wear correction
      var toolAxisOffsetCorrected = toolAxisOffset + correctionAmount;

      var offsetTo = macro.FormatD(toolAxisOffsetCorrected);
      var offsetFrom = macro.FormatD(toolAxisOffset);
      macro.exec.AddStatusmessage("M32: Setting H" + toolOffsetNumber + " tool offset" + (offsetTo != offsetFrom ? " from " + offsetLabel + offsetFrom : "") + " to Z" + offsetTo);

      // update tool offset field
      macro.AS3.Setfield(toolAxisOffsetCorrected, fieldIdxToolAxisOffset);
      macro.AS3.Validatefield(fieldIdxToolAxisOffset);
      // save tool offset in profile
      macro.exec.Writekey("Tooltablevalues", "Tooloffset" + offsetLabel + toolOffsetNumber, toolAxisOffsetCorrected.ToString());

      return true;
    }

    protected bool ProbeCycle(Macroclass macro, ProbeSettings settings, double overtravel, Axis axis, double surfacePositionWCS, out double strikePositionWCS) {
      strikePositionWCS = default(double);

      var currentPositionWCS = macro.GetWCSPosition(axis);

      var direction = surfacePositionWCS > currentPositionWCS ? 1 : -1;

      // two stage probe, fast then fine feed rate
      for (var i = 0; i < 2; i++) {
        // first probe use fast feed, second use fine feed
        var feedRate = i < 1 ? settings.FastFeedRate : settings.FineFeedRate;
        // on first probe use position else use retract distance
        var distance = i < 1 ? macro.Distance(surfacePositionWCS, currentPositionWCS) : settings.Retract;
        distance = direction > 0 ? distance + overtravel : -distance - overtravel;

        if (!macro.ExecuteGCode(
            // start probing
            "G91 F" + macro.FormatD(feedRate) + " G31 " + axis.ToString() + macro.FormatD(distance)
        )) {
          macro.exec.AddStatusmessage("M32: probe routine interrupted");
          return false;
        }

        // check probe status outcome
        var probeStatus = macro.exec.Getvar(5060);
        if (probeStatus == 1) {
          // macro.ExecuteGCode(string.Format("G90 F{2} G1 {0}{1}", axis, macro.FormatD(currentPositionWCS), settings.TravelFeedRate));
          macro.exec.AddStatusmessage("M32: probe routine failed to strike surface with-in travel");
          return false;
        }
        else if (probeStatus != 0) {
          // macro.ExecuteGCode(string.Format("G90 F{2} G1 {0}{1}", axis, macro.FormatD(currentPositionWCS), settings.TravelFeedRate));
          macro.exec.AddStatusmessage(string.Format("M32: probe routine failed to strike surface (ERR{0})", probeStatus));
          return false;
        }

        // on first probe use G90 retract distance otherwise restore original G90 position
        distance = direction > 0 ? -settings.Retract : settings.Retract; // inverted to move away from strike
        if (!macro.ExecuteGCode(
             "G" + (i < 1 ? 91 : 90) + " F" + settings.TravelFeedRate + " G1 " + axis.ToString() + macro.FormatD((i < 1 ? distance : currentPositionWCS))
        )) {
          macro.exec.AddStatusmessage("M32: probe routine interrupted");
          return false;
        }
      }

      strikePositionWCS = axis == Axis.X ? macro.exec.Getvar(5061) :
                              axis == Axis.Y ? macro.exec.Getvar(5062) :
                              macro.exec.Getvar(5063);

      if (axis != Axis.Z) {
        // compensate for probe diameter
        strikePositionWCS = direction > 0 ? strikePositionWCS + (settings.Diameter * 0.5D) : strikePositionWCS - (settings.Diameter * 0.5D);
      }

      return true;
    }
  }

  public class SurfaceProbeRoutine : ProbeRoutineBase {
    public SurfaceProbeRoutine() : base() {
      // Q, W, O added by ProbeRoutineBase
      this.Inputs.AddRange(new[]{
            new ProbeInput('X', "Nominal position of feature", true, new ProbeInputGroup(new []{'Y', 'Z'})),
            new ProbeInput('Y', "Nominal position of feature", true, new ProbeInputGroup(new []{'X', 'Z'})),
            new ProbeInput('Z', "Nominal position of feature", true, new ProbeInputGroup(new []{'X', 'Y'})),

            new ProbeInput('N', "Feature position tolerance", false, false, 0, null),

            new ProbeInput('H', "Tool offset number to update", false),
            new ProbeInput('K', "Tool offset apply precentage", false, false, 0, 1, new ProbeInputGroup(null, new []{'H'})),
            new ProbeInput('V', "Tool offset dead zone", false, false, 0, null, new ProbeInputGroup(null, new []{'H'}))
        });
    }

    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.Surface;
      }
    }

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();

      var axisLabel = inputs.ContainsKey('X') ? 'X' : inputs.ContainsKey('Y') ? 'Y' : 'Z';
      var axis = (Axis)Enum.Parse(typeof(Axis), axisLabel.ToString());
      var positionNominalWCS = inputs[axisLabel];

      var overtravel = inputs['Q'];
      var positionTolerance = inputs.ContainsKey('N') ? (double?)inputs['N'] : null;
      var wcsIndex = inputs.ContainsKey('W') ? (int?)inputs['W'] : null;

      double positionStrikeWCS;
      if (!this.ProbeCycle(macro, settings, overtravel, axis, positionNominalWCS, out positionStrikeWCS)) {
        return false;
      }

      var positionError = positionStrikeWCS - positionNominalWCS;
      var outOfTolerance = this.AssertTolerance(macro, axis.ToString() + " position", positionTolerance, positionError) ? 0D : 1D;

      outputs.Add(ProbeOutputNum.XPosition, axis == Axis.X ? positionStrikeWCS : 0D);
      outputs.Add(ProbeOutputNum.YPosition, axis == Axis.Y ? positionStrikeWCS : 0D);
      outputs.Add(ProbeOutputNum.ZPosition, axis == Axis.Z ? positionStrikeWCS : 0D);
      outputs.Add(ProbeOutputNum.XPositionError, axis == Axis.X ? positionError : 0D);
      outputs.Add(ProbeOutputNum.YPositionError, axis == Axis.Y ? positionError : 0D);
      outputs.Add(ProbeOutputNum.ZPositionError, axis == Axis.Z ? positionError : 0D);
      outputs.Add(ProbeOutputNum.PositionError, positionError);
      outputs.Add(ProbeOutputNum.OutOfTolerance, outOfTolerance);

      this.AppendToReport(macro, inputs.ContainsKey('O') ? (int?)inputs['O'] : null, outputs);

      if (outOfTolerance > 0) {
        return false;
      }

      if (!this.UpdateToolWear(macro, axis, positionError, inputs)) {
        return false;
      }

      if (!this.UpdateWCS(macro, wcsIndex, inputs, new Tuple<double, double>[]{
            axis == Axis.X ? new Tuple<double, double>(positionStrikeWCS, positionError) : null,
            axis == Axis.Y ? new Tuple<double, double>(positionStrikeWCS, positionError) : null,
            axis == Axis.Z ? new Tuple<double, double>(positionStrikeWCS, positionError) : null
        })) {
        return false;
      }

      return true;
    }
  }

  public class PocketWebProbeRoutine : ProbeRoutineBase {
    public PocketWebProbeRoutine() {
      // Q, W, O added by ProbeRoutineBase
      this.Inputs.AddRange(new[]{
            new ProbeInput('X', "Nominal axis dimension of feature", true, new ProbeInputGroup(new []{'Y'})),
            new ProbeInput('Y', "Nominal axis dimension of feature", true, new ProbeInputGroup(new []{'X'})),
            new ProbeInput('Z', "Z-axis position of feature", false),
            new ProbeInput('R', "Axis clearance of feature", false),
            new ProbeInput('N', "Position tolerance", false, false, 0, null),
            new ProbeInput('E', "Dimension [width] tolerance", false, false, 0, null),

            new ProbeInput('H', "Tool offset number to update", false),
            new ProbeInput('K', "Tool offset apply precentage", false, false, 0, 1, new ProbeInputGroup(null, new []{'H'})),
            new ProbeInput('V', "Tool offset dead zone", false, false, 0, null, new ProbeInputGroup(null, new []{'H'}))
        });
    }

    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.PocketOrWeb;
      }
    }

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();

      var axisLabel = inputs.ContainsKey('X') ? 'X' : 'Y';
      var axis = (Axis)Enum.Parse(typeof(Axis), axisLabel.ToString());
      var sizeNominal = inputs[axisLabel];
      var sizeHalfNominal = sizeNominal * 0.5D;

      var positionZWCS = inputs.ContainsKey('Z') ? (double?)inputs['Z'] : null;
      var clearanceRel = inputs.ContainsKey('R') ? (double?)inputs['R'] : null;

      var overtravel = inputs['Q'];

      var positionTolerance = inputs.ContainsKey('N') ? (double?)inputs['N'] : null;
      var sizeTolerance = inputs.ContainsKey('E') ? (double?)inputs['E'] : null;

      var wcsIndex = inputs.ContainsKey('W') ? (int?)inputs['W'] : null;

      double positionOriginalWCS = macro.GetWCSPosition(axis);
      double positionZOriginalWCS = macro.GetWCSPosition(Axis.Z);

      if (!macro.ExecuteGCode(
          "M213",
          "F" + settings.TravelFeedRate,
          // if has clearance, move relative half nominal + clearance
          clearanceRel != null ? ("G91 G1 " + axis.ToString() + macro.FormatD(-(sizeHalfNominal + clearanceRel.Value))) : "",
          // if has Z position, move to it
          positionZWCS != null ? ("G90 G1 Z" + macro.FormatD(positionZWCS.Value)) : "",
          "M214"
      )) {
        return false;
      }

      double positionCurrentWCS = macro.GetWCSPosition(axis);

      double positionStrike1WCS;
      double positionSurface1WCS = clearanceRel != null ? positionCurrentWCS + clearanceRel.Value : positionCurrentWCS - sizeHalfNominal;
      if (!this.ProbeCycle(macro, settings, overtravel, axis, positionSurface1WCS, out positionStrike1WCS)) {
        return false;
      }

      if (!macro.ExecuteGCode(
          "M213",
          "F" + settings.TravelFeedRate,
          // if has Z position, restore to original
          positionZWCS != null ? ("G90 G1 Z" + macro.FormatD(positionZOriginalWCS)) : "",
          // go back to original position
          "G90 G1 " + axis.ToString() + macro.FormatD(positionOriginalWCS),
          // if has clearance, move relative half nominal + clearance
          clearanceRel != null ? ("G91 G1 " + axis.ToString() + macro.FormatD(sizeHalfNominal + clearanceRel.Value)) : "",
          // if has Z position, move to it
          positionZWCS != null ? ("G90 G1 Z" + macro.FormatD(positionZWCS.Value)) : "",
          "M214"
      )) {
        return false;
      }

      positionCurrentWCS = macro.GetWCSPosition(axis);

      double positionStrike2WCS;
      double positionSurface2WCS = clearanceRel != null ? positionCurrentWCS - clearanceRel.Value : positionCurrentWCS + sizeHalfNominal;
      if (!this.ProbeCycle(macro, settings, overtravel, axis, positionSurface2WCS, out positionStrike2WCS)) {
        return false;
      }

      var sizeDimension = macro.Distance(positionStrike1WCS, positionStrike2WCS);
      var positionCenterWCS = positionStrike1WCS + (sizeDimension * 0.5D);
      var positionError = positionCenterWCS - positionOriginalWCS;
      var sizeError = sizeDimension - sizeNominal;

      if (!macro.ExecuteGCode(
          "M213",
          "F" + macro.FormatD(settings.TravelFeedRate),
          // if has Z position, restore to original
          positionZWCS != null ? ("G90 G1 Z" + macro.FormatD(positionZOriginalWCS)) : "",
          // get to center based on strikes
          "G90 G1 " + axis.ToString() + macro.FormatD(positionCenterWCS),
          "M214"
      )) {
        return false;
      }

      var outOfTolerance = !this.AssertTolerance(macro, axis.ToString() + " position", positionTolerance, positionError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, axis.ToString() + " measured dimension", sizeTolerance, sizeError);

      outputs.Add(ProbeOutputNum.XPosition, axis == Axis.X ? positionCenterWCS : 0D);
      outputs.Add(ProbeOutputNum.YPosition, axis == Axis.Y ? positionCenterWCS : 0D);
      outputs.Add(ProbeOutputNum.XPositionError, axis == Axis.X ? positionError : 0D);
      outputs.Add(ProbeOutputNum.YPositionError, axis == Axis.Y ? positionError : 0D);
      outputs.Add(ProbeOutputNum.XDimension, axis == Axis.X ? sizeDimension : 0D);
      outputs.Add(ProbeOutputNum.YDimension, axis == Axis.Y ? sizeDimension : 0D);
      outputs.Add(ProbeOutputNum.Dimension, sizeDimension);
      outputs.Add(ProbeOutputNum.XDimensionError, axis == Axis.X ? sizeError : 0D);
      outputs.Add(ProbeOutputNum.YDimensionError, axis == Axis.Y ? sizeError : 0D);
      outputs.Add(ProbeOutputNum.PositionError, positionError);
      outputs.Add(ProbeOutputNum.DimensionError, sizeError);
      outputs.Add(ProbeOutputNum.OutOfTolerance, outOfTolerance ? 1D : 0D);

      this.AppendToReport(macro, inputs.ContainsKey('O') ? (int?)inputs['O'] : null, outputs);

      if (outOfTolerance) {
        return false;
      }

      if (!this.UpdateToolWear(macro, axis, sizeError * 0.5D, inputs)) {
        return false;
      }

      if (!this.UpdateWCS(macro, wcsIndex, inputs, new Tuple<double, double>[]{
            axis == Axis.X ? new Tuple<double, double>(positionCenterWCS, positionError) : null,
            axis == Axis.Y ? new Tuple<double, double>(positionCenterWCS, positionError) : null
        })) {
        return false;
      }

      return true;
    }
  }


  public class BoreBossProbeRoutine : ProbeRoutineBase {
    public BoreBossProbeRoutine() {
      // Q, W, O added by ProbeRoutineBase
      this.Inputs.AddRange(new[]{
            new ProbeInput('D', "Nominal dimension of feature", true),
            new ProbeInput('Z', "Z-axis position of measurement", false),
            new ProbeInput('R', "Axis clearance of feature", false),

            new ProbeInput('N', "Position tolerance", false, false, 0, null),
            new ProbeInput('E', "Dimension [diameter] tolerance", false, false, 0, null),

            new ProbeInput('H', "Tool offset number to update", false),
            new ProbeInput('K', "Tool offset apply precentage", false, false, 0, 1, new ProbeInputGroup(null, new []{'H'})),
            new ProbeInput('V', "Tool offset dead zone", false, false, 0, null, new ProbeInputGroup(null, new []{'H'}))
        });
    }

    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.BoreOrBoss;
      }
    }

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();

      // using PocketWeb routine in both X and Y
      var subroutine = ProbeRoutines[ProbeRoutine.PocketOrWeb];
      var subroutineInputs = new Dictionary<char, double>();

      var sizeNominal = inputs['D'];
      var sizeHalfNominal = sizeNominal * 0.5D;

      var positionTolerance = inputs.ContainsKey('N') ? (double?)inputs['N'] : null;
      var positionTrueTolerance = inputs.ContainsKey('L') ? (double?)inputs['L'] : null;
      var sizeTolerance = inputs.ContainsKey('E') ? (double?)inputs['E'] : null;
      var wcsIndex = inputs.ContainsKey('W') ? (int?)inputs['W'] : null;

      subroutineInputs.Add('Q', inputs['Q']);
      if (inputs.ContainsKey('Z')) {
        subroutineInputs.Add('Z', inputs['Z']);
      }
      if (inputs.ContainsKey('R')) {
        subroutineInputs.Add('R', inputs['R']);
      }

      double positionXOriginalWCS = macro.GetWCSPosition(Axis.X);
      double positionYOriginalWCS = macro.GetWCSPosition(Axis.Y);

      Dictionary<ProbeOutputNum, double> outputsX;
      subroutineInputs.Add('X', sizeNominal);
      var result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsX);
      if (!result) {
        return false;
      }

      Dictionary<ProbeOutputNum, double> outputsY;
      subroutineInputs.Remove('X');
      subroutineInputs.Add('Y', sizeNominal);
      result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsY);
      if (!result) {
        return false;
      }

      // TODO: ability to disable second cycle on X
      if (true) {
        // do second cycle on X after centering
        subroutineInputs.Remove('Y');
        subroutineInputs.Add('X', sizeNominal);
        result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsX);
        if (!result) {
          return false;
        }
      }

      var positionXCenterWCS = outputsX[ProbeOutputNum.XPosition];
      var positionYCenterWCS = outputsY[ProbeOutputNum.YPosition];
      var sizeXDimension = outputsX[ProbeOutputNum.XDimension];
      var sizeYDimension = outputsY[ProbeOutputNum.YDimension];

      var positionTrueError = macro.Distance(positionXOriginalWCS, positionYOriginalWCS, positionXCenterWCS, positionYCenterWCS);
      var positionXError = positionXCenterWCS - positionXOriginalWCS;
      var positionYError = positionYCenterWCS - positionYOriginalWCS;
      var positionError = macro.MaxDifference(positionXError, positionYError);

      var sizeXError = sizeXDimension - sizeNominal;
      var sizeYError = sizeYDimension - sizeNominal;
      var sizeDimension = Math.Max(sizeXDimension, sizeYDimension);
      var sizeError = macro.MaxDifference(sizeXError, sizeYError);

      var outOfTolerance = !this.AssertTolerance(macro, "X position", positionTolerance, positionXError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "Y position", positionTolerance, positionYError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "true position", positionTrueTolerance, positionTrueError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "measured dimension", sizeTolerance, sizeError);

      outputs.Add(ProbeOutputNum.XPosition, positionXCenterWCS);
      outputs.Add(ProbeOutputNum.YPosition, positionYCenterWCS);
      outputs.Add(ProbeOutputNum.XPositionError, positionXError);
      outputs.Add(ProbeOutputNum.YPositionError, positionYError);
      outputs.Add(ProbeOutputNum.XDimension, sizeXDimension);
      outputs.Add(ProbeOutputNum.YDimension, sizeYDimension);
      outputs.Add(ProbeOutputNum.Dimension, sizeDimension);
      outputs.Add(ProbeOutputNum.XDimensionError, sizeXError);
      outputs.Add(ProbeOutputNum.YDimensionError, sizeYError);
      outputs.Add(ProbeOutputNum.TruePositionError, positionTrueError);
      outputs.Add(ProbeOutputNum.PositionError, positionError);
      outputs.Add(ProbeOutputNum.DimensionError, sizeError);
      outputs.Add(ProbeOutputNum.OutOfTolerance, outOfTolerance ? 1D : 0D);

      this.AppendToReport(macro, inputs.ContainsKey('O') ? (int?)inputs['O'] : null, outputs);

      if (outOfTolerance) {
        return false;
      }

      if (!this.UpdateToolWear(macro, Axis.X, sizeError * 0.5D, inputs)) {
        return false;
      }

      if (!this.UpdateWCS(macro, wcsIndex, inputs, new Tuple<double, double>[]{
            new Tuple<double, double>(positionXCenterWCS, positionXError),
            new Tuple<double, double>(positionYCenterWCS, positionYError)
        })) {
        return false;
      }

      return result;
    }
  }

  public class BoreBoss3PointProbeRoutine : ProbeRoutineBase {
    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.BoreOrBoss3Point;
      }
    }

    /*
    public override string Help() {
      return String.Join("\n", new[]{
            "P" + (int)this.Routine + " (Bore/Boss 3-Point Probe)",
            "Z (Z-axis position of measurement with boss feature)",
            "A (1st angle of measurement vector)",
            "B (2nd angle of measurement vector)",
            "C (3rd angle of measurement vector)",
            "D (Nominal measurement [size] of feature)",
            "Q (Probe overtravel threshold)",
            "R (Retract clearance of feature surfaces)",
            "N (Position tolerance)",
            "E (Measurement [size] tolerance)",
            "W (Work coordinate system to update [0 = active, 1-6=G54-G59])",
            "O (Increment feature number [1] or Increment component number and reset feature number [2])"
        });
    }
    */

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();
      macro.exec.AddStatusmessage(string.Format("M32: P{0} Not Implemented", (int)this.Routine));
      return false;
    }
  }

  public class InternalCornerProbeRoutine : ProbeRoutineBase {
    public InternalCornerProbeRoutine() {
      // Q, W, O added by ProbeRoutineBase
      this.Inputs.AddRange(new[]{
            new ProbeInput('X', "Nominal axis position of feature", true),
            new ProbeInput('Y', "Nominal axis position of feature", true),
            new ProbeInput('I', "Incermental X-axis distance of secondary probe", false),
            new ProbeInput('J', "Incermental Y-axis distance of secondary probe", false),

            new ProbeInput('N', "Position tolerance", false, false, 0, null),
            new ProbeInput('L', "True position tolerance", false, false, 0, null),
            new ProbeInput('B', "Dimension [angle] tolerance", false, false, 0, null, new ProbeInputGroup(null, new char[]{'I', 'J'})),
        });
    }

    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.InternalCorner;
      }
    }

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();

      // using Surface routine
      var subroutine = ProbeRoutines[ProbeRoutine.Surface];
      var subroutineInputs = new Dictionary<char, double>();

      var positionXNominal = inputs['X'];
      var positionYNominal = inputs['Y'];

      var incrementX = inputs.ContainsKey('I') ? (double?)inputs['I'] : null;
      var incrementY = inputs.ContainsKey('J') ? (double?)inputs['J'] : null;

      var positionTolerance = inputs.ContainsKey('N') ? (double?)inputs['N'] : null;
      double? positionTrueTolerance = inputs.ContainsKey('L') ? (double?)inputs['L'] : null;
      var angleTolerance = inputs.ContainsKey('B') ? (double?)inputs['B'] : null;
      var wcsIndex = inputs.ContainsKey('W') ? (int?)inputs['W'] : null;

      subroutineInputs.Add('Q', inputs['Q']);

      double positionXOriginalWCS = macro.GetWCSPosition(Axis.X);
      double positionYOriginalWCS = macro.GetWCSPosition(Axis.Y);

      Dictionary<ProbeOutputNum, double> outputsX;
      subroutineInputs.Add('X', positionXNominal);
      var result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsX);
      if (!result) {
        return false;
      }
      var positionX1WCS = outputsX[ProbeOutputNum.XPosition];
      var positionX2WCS = positionX1WCS;

      if (incrementY != null) {
        // move to secondary probe position
        if (!macro.ExecuteGCode(
          "G90 F" + macro.FormatD(settings.TravelFeedRate) + " G1 X" + macro.FormatD(positionXOriginalWCS) + " Y" + macro.FormatD(positionYOriginalWCS),
          "G91 F" + macro.FormatD(settings.TravelFeedRate) + " G1 Y" + macro.FormatD(incrementY)
        )) {
          return false;
        }

        result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsX);
        if (!result) {
          return false;
        }
        positionX2WCS = outputsX[ProbeOutputNum.XPosition];

        // move back to original position
        if (!macro.ExecuteGCode(
          "G90 F" + macro.FormatD(settings.TravelFeedRate) + " G1 X" + macro.FormatD(positionXOriginalWCS) + " Y" + macro.FormatD(positionYOriginalWCS)
        )) {
          return false;
        }
      }

      Dictionary<ProbeOutputNum, double> outputsY;
      subroutineInputs.Remove('X');
      subroutineInputs.Add('Y', positionYNominal);
      result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsY);
      if (!result) {
        return false;
      }
      var positionY1WCS = outputsY[ProbeOutputNum.YPosition];
      var positionY2WCS = positionY1WCS;

      if (incrementX != null) {
        // move to secondary probe position
        if (!macro.ExecuteGCode(
          "G90 F" + macro.FormatD(settings.TravelFeedRate) + " G1 X" + macro.FormatD(positionXOriginalWCS) + " Y" + macro.FormatD(positionYOriginalWCS),
          "G91 F" + macro.FormatD(settings.TravelFeedRate) + " G1 X" + macro.FormatD(incrementX)
        )) {
          return false;
        }

        result = subroutine.InternalExecute(macro, settings, subroutineInputs, out outputsY);
        if (!result) {
          return false;
        }
        positionY2WCS = outputsY[ProbeOutputNum.YPosition];

        // move back to original position
        if (!macro.ExecuteGCode(
          "G90 F" + macro.FormatD(settings.TravelFeedRate) + " G1 X" + macro.FormatD(positionXOriginalWCS) + " Y" + macro.FormatD(positionYOriginalWCS)
        )) {
          return false;
        }
      }

      double positionX2SecondaryWCS = positionXOriginalWCS + (incrementX ?? 1D);
      double positionY2SecondaryWCS = positionYOriginalWCS + (incrementY ?? 1D);

      double positionXCornerWCS;
      double positionYCornerWCS;
      if (incrementY != null && incrementX == null) {
        // get perpendicular point from Y-axis line and X-axis point
        macro.Perpendicular(
          positionX1WCS, positionYOriginalWCS, positionX2WCS, positionX2SecondaryWCS,
          positionXOriginalWCS, positionY1WCS,
          out positionX2SecondaryWCS, out positionY2WCS
        );
      }

      else if (incrementX != null && incrementY == null) {
        // get perpendicular point from X-axis line and Y-axis point
        macro.Perpendicular(
          positionXOriginalWCS, positionY1WCS, positionX2SecondaryWCS, positionY2WCS,
          positionX1WCS, positionYOriginalWCS,
          out positionX2WCS, out positionY2SecondaryWCS
        );
      }

      // intersection from XY axis lines
      if (!macro.Intersection(
        positionX1WCS, positionYOriginalWCS, positionX2WCS, positionY2SecondaryWCS,
        positionXOriginalWCS, positionY1WCS, positionX2SecondaryWCS, positionY2WCS,
        out positionXCornerWCS, out positionYCornerWCS
      )) {
        macro.exec.AddStatusmessage("M32: Failed to calculate corner position");
        return false;
      }

      var positionTrueError = macro.Distance(positionXNominal, positionYNominal, positionXCornerWCS, positionYCornerWCS);
      var positionXError = positionXCornerWCS - positionXNominal;
      var positionYError = positionYCornerWCS - positionYNominal;
      var positionError = macro.MaxDifference(positionXError, positionYError);

      var angleXDimension = positionX2WCS > positionX1WCS ?
                              macro.AngleRadians(positionX2WCS, positionY2SecondaryWCS, positionX1WCS, positionYOriginalWCS) :
                              macro.AngleRadians(positionX1WCS, positionYOriginalWCS, positionX2WCS, positionY2SecondaryWCS);
      var angleYDimension = positionY2WCS < positionY1WCS ?
                              macro.AngleRadians(positionX2SecondaryWCS, positionY2WCS, positionXOriginalWCS, positionY1WCS) :
                              macro.AngleRadians(positionXOriginalWCS, positionY1WCS, positionX2SecondaryWCS, positionY2WCS);

      var angleDimension = macro.AngleRadians(angleXDimension, angleYDimension);
      angleXDimension = macro.RadiansToDegrees(angleXDimension);
      angleYDimension = macro.RadiansToDegrees(angleYDimension);
      angleDimension = Math.Abs(macro.RadiansToDegrees(angleDimension));

      // angleXDimension = Math.Abs(angleXDimension) > 90D ? angleXDimension - 90 : angleXDimension;
      // angleXDimension = angleXDimension > 90D ? angleXDimension - 90D : angleXDimension < -90D ? angleXDimension + 90D : angleXDimension;
      // angleYDimension = angleYDimension > 90D ? angleYDimension - 90D : angleYDimension < -90D ? angleYDimension + 90D : angleYDimension;

      var angleXError = angleXDimension > 0 ? angleXDimension - 90D : angleXDimension + 90D;
      var angleYError = angleYDimension;
      var angleError = angleDimension > 0 ? angleDimension - 90D : angleDimension + 90D;

      var outOfTolerance = !this.AssertTolerance(macro, "X position", positionTolerance, positionXError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "Y position", positionTolerance, positionYError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "true position", positionTrueTolerance, positionTrueError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "measured X dimension", angleTolerance, angleXError);
      outOfTolerance = outOfTolerance ? outOfTolerance : !this.AssertTolerance(macro, "measured Y dimension", angleTolerance, angleYError);

      outputs.Add(ProbeOutputNum.XPosition, positionXCornerWCS);
      outputs.Add(ProbeOutputNum.YPosition, positionYCornerWCS);
      outputs.Add(ProbeOutputNum.XPositionError, positionXError);
      outputs.Add(ProbeOutputNum.YPositionError, positionYError);
      outputs.Add(ProbeOutputNum.XDimension, angleXDimension);
      outputs.Add(ProbeOutputNum.YDimension, angleYDimension);
      outputs.Add(ProbeOutputNum.XDimensionError, angleXError);
      outputs.Add(ProbeOutputNum.YDimensionError, angleYError);
      outputs.Add(ProbeOutputNum.TruePositionError, positionTrueError);
      outputs.Add(ProbeOutputNum.PositionError, positionError);
      outputs.Add(ProbeOutputNum.OutOfTolerance, outOfTolerance ? 1D : 0D);
      outputs.Add(ProbeOutputNum.Dimension, angleDimension);
      outputs.Add(ProbeOutputNum.DimensionError, angleError);

      this.AppendToReport(macro, inputs.ContainsKey('O') ? (int?)inputs['O'] : null, outputs);

      if (outOfTolerance) {
        return false;
      }

      if (!this.UpdateWCS(macro, wcsIndex, inputs, new Tuple<double, double>[]{
            new Tuple<double, double>(positionXCornerWCS, positionXError),
            new Tuple<double, double>(positionYCornerWCS, positionYError)
        })) {
        return false;
      }

      return result;
    }
  }

  public class ExternalCornerProbeRoutine : ProbeRoutineBase {
    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.ExternalCorner;
      }
    }

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();
      macro.exec.AddStatusmessage(string.Format("M32: P{0} Not Implemented", (int)this.Routine));
      return false;
    }
  }

  public class SurfaceAngleProbeRoutine : ProbeRoutineBase {
    public override ProbeRoutine Routine {
      get {
        return ProbeRoutine.SurfaceAngle;
      }
    }

    internal override bool InternalExecute(Macroclass macro, ProbeSettings settings, Dictionary<char, double> inputs, out Dictionary<ProbeOutputNum, double> outputs) {
      outputs = new Dictionary<ProbeOutputNum, double>();
      macro.exec.AddStatusmessage(string.Format("M32: P{0} Not Implemented", (int)this.Routine));
      return false;
    }
  }

#if DEBUG_CSX
}
#endif