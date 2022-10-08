using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using KModkit;

public class RGBLightScript : MonoBehaviour {

	public KMBombInfo Bomb;
	public KMAudio Audio;
	public KMBombModule Module;
	public KMColorblindMode Colorblind;

	public Material[] LightColors;//Black, Red, Green, Blue, Cyan, Magenta, Yellow, White
	public KMSelectable[] buttons;
	public Renderer[] buttonLights;
	public KMSelectable submitButton;
	public KMSelectable[] toggleButtons;
	public Renderer[] toggleLights;
	public Renderer cagedLight;

	public TextMesh cageLEDCBText;
	public TextMesh[] cbTexts;

	public GameObject ColorblindDisplays;

	private string[] cbColors = new string[] { "", "R", "G", "B", "C", "M", "Y", "" };

	//-----------------------------------------------------//
	//READONLY LIBRARIES
	private char[] LightColorsDebugChar = new char[] {'K', 'R', 'G', 'B', 'C', 'M', 'Y', 'W'};
	private int[,,] lightTable = new int[,,] {//Used with [X, Y, Z] to determine a color
		{//RED==0
			{//GREEN==0
				0,//BLUE==0
				3//BLUE==1
			},
			{//GREEN==1
				2,//BLUE==0
				4//BLUE==1
			}
		},
		{//RED==1
			{//GREEN==0
				1,//BLUE==0
				5//BLUE==1
			},
			{//GREEN==1
				6,//BLUE==0
				7//BLUE==1
			}
		}
	};
	private int[,] RGBCycle = new int[,] {//The 3 RGB settings
		{1, 0, 0}, {0, 1, 0}, {0, 0, 1}
	};
	private char[] alphabet = new char[] {//The Alphabet
		'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
	  // 1    2    3    4    5    6    7    8    9    10   11   12   13   14   15   16   17   18   19   20   21   22   23   24   25   26
	};

	//MODIFIER BUTTONS
	private int[,] toggleSets = new int[,] { { 0, 3 }, { 1, 2 } };//DO NOT MODIFY
	private int[] buttonOrder = new int[] { 4, 4, 4, 4 };
	private int[] lightSetValues = new int[] { 0, 1, 0, 2 };//What color each set is adding
	private int[,] lightSets = new int[,] {
		{0, 2, 3}, {0, 1, 3}, {0, 1, 2}, {1, 2, 3}
	};
	private int[] toggleActive = new int[] { 0, 0 };

	//VISIBLE LIGHT RGB
	private int[,] lightRGB = new int[,] {//The current lights' RGB values
		{0, 0, 0}, {0, 0, 0},
		{0, 0, 0}, {0, 0, 0}
	};
	private int[] cagedRGB = new int[] { 0, 0, 0 };//The caged LED's RGB value
	int cagedColor;

	//SOLVE VARIABLES
	private int[] totalColor = new int[] { 0, 0, 0, 0 };
	private int[,] solutionCodesUnmodified = new int[2, 3];//Only used for 2 checks
	private int[] solutionCodes = new int[2];
	private int correctCode = 0;//Which code is the solution
	private int[,] solveChart = new int[,] {
		{0, 1, 3, 0},//BLACK
		{7, 6, 0, 1},//RED
		{6, 2, 2, 6},//GREEN
		{4, 7, 3, 0},//BLUE
		{7, 0, 5, 3},//CYAN
		{3, 4, 6, 1},//MAGENTA
		{2, 5, 6, 3},//YELLOW
		{4, 7, 7, 6}//WHITE
	};
	private int[] solveColor = new int[] { 0, 0, 0, 0 };
	//-----------------------------------------------------//

	//Required
	private bool setupFin = false;

	//DEBUG
	//IEnumerable<int> serialNums = Bomb.GetSerialNumberNumbers();

	//Logging (Goes at the bottom of global variables)
	static int moduleIdCounter = 1;
	int moduleId;

	private bool cbActive;
	private bool moduleSolved;

	private void Awake() {
		moduleId = moduleIdCounter++;
		foreach (KMSelectable NAME in buttons) {
			KMSelectable pressedObject = NAME;
			NAME.OnInteract += delegate () { PressButton(pressedObject); return false; };
		}
		/*
		buttons[0].OnInteract += delegate () { PressButton(buttonOrder[0]); return false; };//DEBUG
		buttons[1].OnInteract += delegate () { PressButton(buttonOrder[1]); return false; };//DEBUG
		buttons[2].OnInteract += delegate () { PressButton(buttonOrder[2]); return false; };//DEBUG
		buttons[3].OnInteract += delegate () { PressButton(buttonOrder[3]); return false; };//DEBUG
		*/
		submitButton.OnInteract += delegate () { Submit(); return false; };
		toggleButtons[0].OnInteract += delegate () { ToggleAlt(0); return false; };
		toggleButtons[1].OnInteract += delegate () { ToggleAlt(1); return false; };

		cbActive = Colorblind.ColorblindModeActive;

		if (cbActive) {ColorblindDisplays.SetActive(true);} else {ColorblindDisplays.SetActive(false);}
	}

	void Start() {

		for (var i = 0; i < 4; i++)
		{
			cbTexts[i].text = "";
		}
		cageLEDCBText.text = "";
		if (!setupFin) {
			Debug.LogFormat("[Rust.G.B. #{0}] Note: colors shown in current and solution logs are in English reading order", moduleId);
			setupButtons();
			setupLayout();
			setupSolution();
			renderLights();
			//setupList();
			logOut();
			setupFin = true;
		}
	}

	void resetModifier() {
		setupSolution();
		renderLights();
		logOut();
	}

	void setupButtons() {
		int[] values = new int[] { 0, 1, 2, 3 };
		for (int x = 0; x < 4; x++) {
			int hold = values[UnityEngine.Random.Range(0, values.Length)];
			buttonOrder[x] = hold;
			values = values.Where(val => val != hold).ToArray();
		}
		Debug.LogFormat("[Rust.G.B. #{0}] Button order [{1}{2}{3}{4}]", moduleId, buttonOrder[0]+1, buttonOrder[1]+1, buttonOrder[2]+1, buttonOrder[3]+1);
	}

	void setupLayout() {
		for(int x = 0; x < 4; x++) {
			lightSetValues[x] = UnityEngine.Random.Range(0, 3);
		}
		for (int x = 0; x < 3; x++) {
			for (int y = 0; y < 3; y++) {
				for (int z = 0; z < 4; z++) {
					lightRGB[lightSets[z, y], x] += RGBCycle[lightSetValues[z], x];
					if (lightRGB[lightSets[z, y], x] == 2) {
						lightRGB[lightSets[z, y], x] = 0;
					}
				}
			}
		}

	}

	void setupSolution() {
		for (int x = 0; x < 3; x++) {
			cagedRGB[x] = UnityEngine.Random.Range(0, 2);
		}
		cagedLight.material = LightColors[lightTable[cagedRGB[0], cagedRGB[1], cagedRGB[2]]];
		cagedColor = lightTable[cagedRGB[0], cagedRGB[1], cagedRGB[2]];

		cageLEDCBText.text = cbActive ? cbColors[lightTable[cagedRGB[0], cagedRGB[1], cagedRGB[2]]].ToString() : "";

		solutionCodesUnmodified = getSerialCodes();
		/*DEBUG//
		solutionCodesUnmodified = new int[,] { {1, 1, 1}, {0, 1, 0} };
		cagedRGB = new int[] {1, 0, 0};
		//DEBUG*/
		solutionCodes = combineColors(solutionCodesUnmodified, cagedRGB);
		conditionCheck();

		int set = solutionCodes[correctCode];
		for (int x = 0; x < 4; x++) {
			solveColor[x] = solveChart[set,x];
		}
	}

	int[,] getSerialCodes() {
		int[,] serialNumValues = new int[2, 3];
		string serialNum = Bomb.GetSerialNumber();
		int charValue = 0;
		for (int x = 0; x < 2; x++) {
			for (int y = 0; y < 3; y++) {
				int z = (2 * y) + x;
				if (Regex.IsMatch(serialNum[z].ToString(), "[0-9]")) {//serialNum[y] matches numCheck
					charValue = ((serialNum[z] - '0')) % 2;
					//Debug.Log(serialNum[z] + " -> " + charValue);
				} else {
					charValue = (Array.IndexOf(alphabet, serialNum[z]) + 1) % 2;
					//Debug.Log(serialNum[z] + " -> " + (Array.IndexOf(alphabet, serialNum[z]) + 1) + " -> " + ((Array.IndexOf(alphabet, serialNum[z]) + 1) % 2));
				}
				serialNumValues[x, y] = charValue;
			}
		}
		if (!setupFin) {
			Debug.LogFormat("[Rust.G.B. #{0}] Set 1 [{1}{2}{3}] -> [{4}{5}{6}]", moduleId, serialNum[0], serialNum[2], serialNum[4], serialNumValues[0, 0], serialNumValues[0, 1], serialNumValues[0, 2]);
			Debug.LogFormat("[Rust.G.B. #{0}] Set 2 [{1}{2}{3}] -> [{4}{5}{6}]", moduleId, serialNum[1], serialNum[3], serialNum[5], serialNumValues[1, 0], serialNumValues[1, 1], serialNumValues[1, 2]);
		}
		Debug.LogFormat("[Rust.G.B. #{0}] Modifier is [{1}{2}{3}]", moduleId, cagedRGB[0], cagedRGB[1], cagedRGB[2]);
		return serialNumValues;
	}

	int[] combineColors(int[,] codeColors, int[] cageColor) {
		int[] OUT = new int[3];
		int[,] HOLD = (int[,])codeColors.Clone();
		for (int x = 0; x < 2; x++) {
			for (int y = 0; y < 3; y++) {
				HOLD[x, y] += cageColor[y];
				if (HOLD[x, y] == 2) {
					HOLD[x, y] = 0;
				}
			}
		}
		for (int x = 0; x < 2; x++) {
			OUT[x] = lightTable[HOLD[x, 0], HOLD[x, 1], HOLD[x, 2]];
		}
		return OUT;
	}

	void conditionCheck() {
		int firstCode = solutionCodes[0];

		if (firstCode == 0) {       //BLACK /VERIFIED
			if (cagedColor == 0 || (cagedColor >=4 && cagedColor <= 6)) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else if (firstCode == 1) {//RED /VERIFIED
			if (Bomb.GetBatteryCount() > 4) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else if (firstCode == 2) {//GREEN /VERIFIED
			if (greenCheck()) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else if (firstCode == 3) {//BLUE /VERIFIED
			if (blueCheck()) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else if (firstCode == 4) {//CYAN /VERIFIED
			if (cyanCheck()) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else if (firstCode == 5) {//MAGENTA /VERIFIED
			if (magentaCheck()) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else if (firstCode == 6) {//YELLOW /VERIFIED
			if (yellowCheck()) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		} else {                    //WHITE /VERIFIED
			if (cagedColor == 7 || (cagedColor >= 1 && cagedColor <= 3)) {
				correctCode = 0;
			} else {
				correctCode = 1;
			}
		}
	}

	bool greenCheck() {
		if (Bomb.IsIndicatorPresent(Indicator.SND) || Bomb.IsIndicatorPresent(Indicator.CLR) || Bomb.IsIndicatorPresent(Indicator.SIG) || Bomb.IsIndicatorPresent(Indicator.NSA)) {
			return true;
		}
		return false;
	}

	bool blueCheck() {
		string[] names = Bomb.GetModuleNames().ToArray();
		bool OUT = true;
		for (int x = 0; x < names.Length; x++) {
			if (names[x].ContainsIgnoreCase("wire")) {
				OUT = !OUT;
			}
		}
		return OUT;
	}

	bool cyanCheck() {
		bool CHECK = false;
		for(int x = 0; x < 2; x++) {
			int COUNT = 0;
			for (int y = 0; y < 3; y++) {
				if (solutionCodesUnmodified[x, y] + cagedRGB[y] == 1) {
					COUNT += 1;
				}
			}
			if (COUNT == 3) {
				CHECK = true;
			}
		}
		return CHECK;
	}

	bool magentaCheck() {
		if(Bomb.GetPortCount(Port.DVI) + Bomb.GetPortCount(Port.StereoRCA) + Bomb.GetPortCount(Port.RJ45) <= 1) {
			return false;
		}
		return true;
	}

	bool yellowCheck() {
		for(int x = 0; x < 3; x++) {
			if(solutionCodesUnmodified[0, x] + solutionCodesUnmodified[1, x] + cagedRGB[x] == 0) {
				return false;
			}
		}
		return true;
	}

	void PressButton(KMSelectable buttonObject) {//KMSelectable button
		int buttonNum = Array.IndexOf(buttons, buttonObject);
		int buttonSet = buttonOrder[buttonNum];
		//Debug.Log(lightSets[buttonNum, ]);
		Audio.PlaySoundAtTransform("click20", transform);
		buttonObject.AddInteractionPunch();
		for (int x = 0; x < 3; x++) {
			for (int y = 0; y < 3; y++) {
				lightRGB[lightSets[buttonSet, y], x] -= RGBCycle[lightSetValues[buttonSet], x];
				if (lightRGB[lightSets[buttonSet, y], x] == -1) {
					lightRGB[lightSets[buttonSet, y], x] = 1;
				}
			}
		}
		lightSetValues[buttonSet] += 1;
		if (lightSetValues[buttonSet] >= 3) {
			lightSetValues[buttonSet] = 0;
		}
		for (int x = 0; x < 3; x++) {
			for (int y = 0; y < 3; y++) {
				lightRGB[lightSets[buttonSet, y], x] += RGBCycle[lightSetValues[buttonSet], x];
				if (lightRGB[lightSets[buttonSet, y], x] == 2) {
					lightRGB[lightSets[buttonSet, y], x] = 0;
				}
			}
		}
		//debugButtonOut(buttonNum, 0, "set");
		//debugButtonOut(buttonNum, 1, "set");
		//debugButtonOut(buttonNum, 2, "set");
		renderLights();
		Debug.LogFormat("[Rust.G.B. #{0}] Button {1} pressed. Current Colors: [{2}, {3}] [{4}, {5}].", moduleId, buttonNum+1,
			getColorChar(0), getColorChar(1), getColorChar(2), getColorChar(3)
		);
	}

	void ToggleAlt(int buttonNUM) {
		if (buttonNUM == 0) {
			Audio.PlaySoundAtTransform("click17", transform);
		} else {
			Audio.PlaySoundAtTransform("click42", transform);
		}
		GetComponent<KMSelectable>().AddInteractionPunch();
		for (int x = 0; x < 3; x++) {
			for (int y = 0; y < 2; y++) {
				lightRGB[toggleSets[buttonNUM, y], x] += 1;
				if (lightRGB[toggleSets[buttonNUM, y], x] == 2) {
					lightRGB[toggleSets[buttonNUM, y], x] = 0;
				}
			}
		}
		if (toggleActive[buttonNUM] == 0) {
			toggleActive[buttonNUM] = 1;
			toggleLights[buttonNUM].material = LightColors[7];
		} else {
			toggleActive[buttonNUM] = 0;
			toggleLights[buttonNUM].material = LightColors[0];
		}
		renderLights();
	}

	void Submit() {
		Audio.PlaySoundAtTransform("click11", transform);
		GetComponent<KMSelectable>().AddInteractionPunch();
		Debug.LogFormat("[Rust.G.B. #{0}] Submitted: [{1}, {2}] [{3}, {4}]", moduleId, getColorChar(0), getColorChar(1), getColorChar(2), getColorChar(3));
		if (moduleSolved) {
			return;
		}
		for (int x = 0; x < 4; x++) {
			totalColor[x] = 0;
			totalColor[x] += lightTable[lightRGB[x, 0], lightRGB[x, 1], lightRGB[x, 2]];
		}
		if (totalColor[0] == solveColor[0] && totalColor[1] == solveColor[1] && totalColor[2] == solveColor[2] && totalColor[3] == solveColor[3]) {
			moduleSolved = true;
			cagedLight.material = LightColors[solutionCodes[correctCode]];
			GetComponent<KMBombModule>().HandlePass();
			Debug.LogFormat("[Rust.G.B. #{0}] Module Solved.", moduleId);
			cageLEDCBText.text = "";
			for (int i = 0; i < 4; i++)
			{
				cbTexts[i].text = "";
			}
		} else {
			GetComponent<KMBombModule>().HandleStrike();
			Debug.LogFormat("[Rust.G.B. #{0}] Incorect colors. Module striked.", moduleId);
			resetModifier();
		}
	}
	
	void renderLights() {
		for (int x = 0; x < 4; x++) {
			buttonLights[x].material = LightColors[lightTable[lightRGB[x, 0], lightRGB[x, 1], lightRGB[x, 2]]];
			cbTexts[x].text = cbActive ? cbColors[lightTable[lightRGB[x,0], lightRGB[x,1], lightRGB[x, 2]]].ToString() : "";
		}
	}

	void logOut() {
		/*
		if (!setupFin) {
			Debug.LogFormat("[Rust.G.B. #{0}] Button sets are [{1}{2}{3}] [{4}{5}{6}] [{7}{8}{9}] [{10}{11}{12}]", moduleId,
				lightSets[buttonOrder[0], 0], lightSets[buttonOrder[0], 1], lightSets[buttonOrder[0], 2],
				lightSets[buttonOrder[1], 0], lightSets[buttonOrder[1], 1], lightSets[buttonOrder[1], 2],
				lightSets[buttonOrder[2], 0], lightSets[buttonOrder[2], 1], lightSets[buttonOrder[2], 2],
				lightSets[buttonOrder[3], 0], lightSets[buttonOrder[3], 1], lightSets[buttonOrder[3], 2]
			);
		}*/
		Debug.LogFormat("[Rust.G.B. #{0}] Solution: [{1}, {2}] [{3}, {4}]", moduleId, LightColorsDebugChar[solveColor[0]], LightColorsDebugChar[solveColor[1]], LightColorsDebugChar[solveColor[2]], LightColorsDebugChar[solveColor[3]]);
		Debug.LogFormat("[Rust.G.B. #{0}] Current Colors: [{1}, {2}] [{3}, {4}]", moduleId, getColorChar(0), getColorChar(1), getColorChar(2), getColorChar(3));
	}

	char getColorChar(int set) {
		return (LightColorsDebugChar[lightTable[lightRGB[set, 0], lightRGB[set, 1], lightRGB[set, 2]]]);
	}

	void Update() {
		/* Useful for:
		   * Updating number of solved modules
		   * 
		 */
	}

	// Twitch Plays Support by Kilo Bites

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} cb to toggle colorblind mode || !{0} press 1-4 1-3 to press the button # times || !{0} invert left/l/right/r to toggle inversions || !{0} submit to submit your answer.";
#pragma warning restore 414

	bool isValidPos(string n)
	{
		string[] valids = { "1", "2", "3", "4" };
		if (!valids.Contains(n))
		{
			return false;
		}
		return true;
	}

	void tpCBMode()
	{
        cageLEDCBText.text = cbActive ? cbColors[lightTable[cagedRGB[0], cagedRGB[1], cagedRGB[2]]].ToString() : "";

		for (int i = 0; i < 4; i++)
		{
            cbTexts[i].text = cbActive ? cbColors[lightTable[lightRGB[i, 0], lightRGB[i, 1], lightRGB[i, 2]]].ToString() : "";
        }
    }

	IEnumerator ProcessTwitchCommand (string command)
	{
		yield return null;

		string[] split = command.ToUpperInvariant().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

		

		if (split[0].EqualsIgnoreCase("CB"))
		{
			cbActive = !cbActive;
			tpCBMode();
			yield break;
		}

		if (split[0].EqualsIgnoreCase("PRESS"))
		{
			int numberClicks = 0;
			int pos = 0;
			if (split.Length != 3)
			{
				yield return "sendtochaterror Please specify which button to press and for how much!";
				yield break;
			}
			else if (!isValidPos(split[1]))
			{
				yield return "sendtochaterror " + split[1] + " is not a valid number!";
				yield break;
			}
			else if (!"123".Any(x => split[2].Contains(x)))
			{
				yield return "sendtochaterror Range must be between 1-3!";
				yield break;
			}
			int.TryParse(split[1], out pos);
			int.TryParse(split[2], out numberClicks);
			pos = pos - 1;

			var presses = 0;
			while (presses != numberClicks)
			{
				buttons[pos].OnInteract();
				presses++;
				yield return new WaitForSeconds(0.1f);
			}
			yield break;
		}
		if (split[0].EqualsIgnoreCase("INVERT"))
		{
			var pos = 0;
			if (split.Length != 2)
			{
				yield return "sendtochaterror Please specify which button to invert!";
				yield break;
			}
			else if (split[1].EqualsIgnoreCase("LEFT") || split[1].EqualsIgnoreCase("L"))
			{
				pos = 0;
			}
			else if (split[1].EqualsIgnoreCase("RIGHT") || split[1].EqualsIgnoreCase("R"))
			{
				pos = 1;
			}
			else
			{
				yield break;
			}

			toggleButtons[pos].OnInteract();

			yield break;
		}

		if (split[0].EqualsIgnoreCase("SUBMIT"))
		{
			submitButton.OnInteract();
			yield break;
		}
	}

	int CheckInvert(int i){
		if (solveColor[i] == 0 || (solveColor[i] >= 4 && solveColor[i] <= 6)){
			return 1;
		}else{
			return 0;
		}
	}

	bool InternalSolveCheck(int[] pressVals, int[,] gridStart, int[] modStart){//Solved?
		int[,] Lights = new int[,] {
			{0, 0, 0}, {0, 0, 0},
			{0, 0, 0}, {0, 0, 0}
		};
		for (int i = 0; i <= 3; i++){
			for (int j = 0; j <= 2; j++){
				Lights[i, j] = gridStart[i, j];
			}
		}
		int[] Mods = new int[] {0, 0, 0, 0};
		for (int i = 0; i <= 3; i++){
			Mods[i] = modStart[i];
		}
		//Debug.Log(pressVals[0] + " " + pressVals[1] + " " + pressVals[2] + " " + pressVals[3]);

		for(int i = 0; i < 4; i++){
			for (int j = 0; j < pressVals[i]; j++){
				for (int x = 0; x < 3; x++) {
					for (int y = 0; y < 3; y++) {
						Lights[lightSets[i, y], x] -= RGBCycle[Mods[i], x];
						if (Lights[lightSets[i, y], x] == -1) {
							Lights[lightSets[i, y], x] = 1;
						}
					}
				}
				Mods[i] += 1;
				if (Mods[i] >= 3) {
					Mods[i] = 0;
				}
				for (int x = 0; x < 3; x++) {
					for (int y = 0; y < 3; y++) {
						Lights[lightSets[i, y], x] += RGBCycle[Mods[i], x];
						if (Lights[lightSets[i, y], x] == 2) {
							Lights[lightSets[i, y], x] = 0;
						}
					}
				}
			}

		}
		int[] testCheckColors = new int[] {0, 0, 0, 0};
		for (int x = 0; x < 4; x++) {
			testCheckColors[x] = 0;
			testCheckColors[x] += lightTable[Lights[x, 0], Lights[x, 1], Lights[x, 2]];
		}
		//Debug.LogFormat(testCheckColors[0] + " " + testCheckColors[1] + " " + testCheckColors[2] + " " + testCheckColors[3] + " == " + solveColor[0] + " " + solveColor[1] + " " + solveColor[2] + " " + solveColor[3]);
		int checkCount = 0;
		for (var i = 0; i < 4; i++){
			if (testCheckColors[i] == solveColor[i]){
				checkCount += 1;
			}
		}
		if (checkCount == 4) return true; else return false;
	}

	IEnumerator TwitchHandleForcedSolve() //Autosolver
	{
		yield return null;

		int tempA = 0;
		while (tempA < 2){
			if (toggleActive[tempA] != CheckInvert(tempA)){
				toggleButtons[tempA].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
			tempA += 1;
		}

		int[] numberPresses = new int[] {0, 0, 0, 0};// Number of times to press each button
		int[,] internalLights = lightRGB;//These two are technically wrong, but won't harm anything
		int[] internalSetValues = lightSetValues;//This one as well

		while (!InternalSolveCheck(numberPresses, internalLights, internalSetValues))//Solved?
		{
			numberPresses[0] += 1;
			if (numberPresses[0] == 3) {
				//Debug.LogFormat("Autosolve paused");
				//yield break;
				numberPresses[0] = 0;
				numberPresses[1] += 1;
				if (numberPresses[1] == 3) {
					numberPresses[1] = 0;
					numberPresses[2] += 1;
					if (numberPresses[2] == 3) {
						numberPresses[2] = 0;
						numberPresses[3] += 1;
						if (numberPresses[3] == 3) {
							numberPresses[3] = 0;
							//Force a crash
							Debug.Log("Autosolve killed: Couldn't find a solution");
							yield break;
						}
					}
				}
			}
		}
		Debug.Log(numberPresses[0] + " " + numberPresses[1] + " " + numberPresses[2] + " " + numberPresses[3]);
		Debug.Log("Button Order [" + buttonOrder[0] + "-" + buttonOrder[1] + "-" + buttonOrder[2] + "-" + buttonOrder[3] + "]");
		for (int i = 0; i < 4; i++){
			Debug.Log("CHECKING POS [" + (i+1) + "] ID [" + buttonOrder[i] + "]");
			for (int j = 0; j < numberPresses[buttonOrder[i]]; j++){
				//Debug.LogFormat("Button ID [" + buttonOrder[i] + "]");
				//buttons[buttonOrder[i]].OnInteract();
				buttons[i].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
		}
		submitButton.OnInteract();
	}
	/*
	FIRST: Pair the button possitions with the correct IDs
	SECOND: Check which invert button needs to be on, turn it on, then never touch them again
	THIRD: 
	
	buttons[pos].OnInteract(); 0, 1, 2, 3 -- coresponds to positions, not ID
	toggleButtons[pos].OnInteract(); 0, 1
	submitButton.OnInteract();
	TwitchPlays command: !1 solve


	PROBLEMS:
	1 - button 1's ID, not position
	2 - 
	3 - 
	4 - 

	
	*/
}
