using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using Valve.VR.InteractionSystem;


public class UIController : MonoBehaviour {

  /** ==============================================================================

    UI for Controlling Program Operations

    Screens
    =============
    1. Top Level
        Leads to all other UI screens

    2. Rotation
      a. Gantry
         i. Left and Right
      b. Bed (6 DOF)
         i. Y axis rotation (90° max)
        ii. X axis rotation ( 3° max)
       iii. Z axis rotation ( 3° max)
        iv. Left/Right Movement
         v. Up/Down Movement
        vi. Forward/Back Movement

    3. Visibility
      a. Lasers
      b. X-Ray
         For each model:
      c. Visibility slider for each part
      d. (Slice view control if completed)

    4. Load/Delete Models
      a. Load Model
        i. List of loadable models
      b. Delete existing model
      c. Delete all

    5. Close UI

  =============================================================================== **/

  //Normal   : Place under a panel
  //Scrolling: All UI elements must appear under content pane of a scroll view
  //  Objects under scroll content, must have absolute size, e.g. Y Pivot 1 1, top = height / 2
  //  Using panel for each object under scroll view seems sensible


  FileReader fr;
  RotationController rc;
  LaserController lc;
  XrayController xc;
  ObjectController oc;

  List<VisControl> visControls = new List<VisControl>();
  ModControl[] modControls = new ModControl[2];
  static Player player;
  int currentPanel = 1;
  int currentItem = 0;
  Dictionary<int,List<GameObject>> selectables = new Dictionary<int,List<GameObject>>();
  Dictionary<int,List<Action<int>>> actions = new Dictionary<int,List<Action<int>>>();
  // Dictionary<int,List<int>> selectableTimers = new Dictionary<int,List<int>>();
  int[] selectableTimers;
  int selectableTimersMax = 500;

  bool showing = false;
  int upCounter = 0;
  int changeMax = 20;
  int downCounter = 0;
  bool changed = false;
  Color lastColour;
  bool colourChanged = false;
  Color highlight = new Color(242/255f,236/255f,39/255f);
  bool delayedHighlight = false;
  int loadDelay = 50;
  int loadCounter = 0;
  //Initial size of scrollview content pane, that must match UI window height
  float contentSize = 217.5f;

  Scrollbar[] sba = new Scrollbar[2];
  RectTransform[] rta = new RectTransform[2];
  Text laserText;
  Text xrayText;

  bool showHints = true;
  bool showingHints = false;

  Hand currentHand;
  List<Hand> hands = new List<Hand>();
  List<Transform> anchors = new List<Transform>();
  bool[] heldObject = new bool[2];
  ControllerHoverHighlight[] controllerHighlights = new ControllerHoverHighlight[2];
  bool[] grabHint = new bool[2];
  


  static int loadingCharLimit = 14;


  float[] rotationSliderValues;

  bool fixScrollbar = false;

  public GameObject[] panels;
  public EventSystem eventSystem;
  public bool keyInput = true;
  public GameObject laserButton;
  public GameObject xrayButton;
  public bool allowXray = true;
  public GameObject loadingText;
  public GameObject queueText;
  public GameObject menuIndicator;

  int touchpadSelection = 0;
  int touchpadCounterMax = 20;
  int touchpadCounter = 0;


  public bool test = false;

  class VisControl {
    public List<GameObject> models;
    public List<Material> mats;
    public float minHeight;
    public float maxHeight;
    public bool open;
    public RectTransform rt;
    public Text text;
  }

  class ModControl {
    public byte type;
    public List<GameObject> models;
    public List<string> files;

    public float minHeight;
    public float maxHeight;
    public bool open;
    public RectTransform rt;
    public Text text;
  }



  void Start() {
    player = Player.instance;

    laserText = laserButton.transform.GetChild(0).GetComponent<Text>();
    xrayText = xrayButton.transform.GetChild(0).GetComponent<Text>();


    oc = GameObject.Find("Object Controller").GetComponent<ObjectController>();
    fr = GameObject.Find("File Controller").GetComponent<FileReader>();
    rc = GameObject.Find("Rotation Controller").GetComponent<RotationController>();
    lc = GameObject.Find("Laser Controller").GetComponent<LaserController>();
    xc = GameObject.Find("X-ray Controller").GetComponent<XrayController>();
    // panels = gameObject.GetComponent<UIController>().panels;
    if (panels.Length == 0) { return; }

    //Get scrollbar and rect transform of panels 3 and 4
    for (int i = 0; i < sba.Length; i++) {
      //1 1
      sba[i] = panels[i+3].transform.GetChild(1).GetChild(1).GetComponent<Scrollbar>();
      //1 0 0
      rta[i] = panels[i+3].transform.GetChild(1).GetChild(0).GetChild(0).GetComponent<RectTransform>();
    }

    SetupUI();
  }

  void Test(UIController ub) {
    ub.test = true;
  }

  public void UpdateUI() {
    // if (currentPanel == 3) {
    RemoveVisPanels();
    CreateVisPanels();
    ToggleVisPanel(-1);
    // } else if (currentPanel == 4) {
    RemoveControlPanels();
    CreateControlPanels();
    ToggleControlPanel(-1);

    GetSelectablesAndActions(currentPanel);
    colourChanged = false;
    SetSelected(currentItem);

    // GameObject g = eventSystem.currentSelectedGameObject;
    // // print("UpdateUI: " + g.name + " " + currentItem);
    // if (g.name.StartsWith("Panel")) {
      // colourChanged = false;
      // int n = currentItem;
      // currentItem = -1;
      // SetSelected(n);
    // }

    // GetSelectablesAndActions(currentPanel);
  }

  void HandleControllerInput() {
    //Input handling
    int h = -1;
    if (!player || player.hands == null) { return; }
    foreach ( Hand hand in player.hands ) {
      h++;
      if (!hand) { continue; }

      // if (showing && hand != currentHand) { continue; }
      if (hand.controller == null) { continue; }

      // if (heldObject[h]) {
        // oc.ConstrainObject(heldObject[h], hand, heldObjectAnchor[h]);
      // }


      if (hand.controller.GetPressUp(SteamVR_Controller.ButtonMask.Grip)) {
        showHints = !showHints;
        continue;
      }

      if (hand.controller.GetPressUp(SteamVR_Controller.ButtonMask.ApplicationMenu)) {
        //App Menu Button
        if (heldObject[0] || heldObject[1]) { continue; }
        if (!showing) {
          currentHand = hand;
        } else {
          currentHand = null;
        }
        ToggleUI(hand);
        showHints = false;
        continue;
      }

      // if (!showing) { continue; }

      //Trigger Button
      if (hand.controller.GetHairTriggerUp()) {
        if (showing && hand == currentHand) {
          //Select Menu Item
          // TriggerItem(currentItem);
          ShowPanel(1);
        } else {
          //Interact with model
          if (heldObject[h]) {
            //Release
            oc.RemoveConstrainedObject(hand.transform);
            heldObject[h] = false;
            // heldObject[h].GetComponent<Rigidbody>().isKinematic = false;
            // heldObject[h].transform.parent = null;
            // heldObject[h] = null;
          } else {
            if (hand.hoveringInteractable) {
              //Pickup
              oc.AddConstrainedObject(hand.hoveringInteractable.gameObject, hand.transform, 
                hand.hoveringInteractable.transform.position - hand.transform.position);
              heldObject[h] = true;
              if (showing) { 
                currentHand = null;
                ToggleUI();
              }
              // heldObject[h] = hand.hoveringInteractable.gameObject;
              // heldObject[h].transform.parent = hand.hoveringInteractable.transform;
              // heldObjectAnchor[h] = heldObject[h].transform.position - hand.transform.position;
              // heldObject[h].GetComponent<Rigidbody>().isKinematic = false;
            }
          }
        }
        showHints = false;
        continue;
      }

      if (hand.controller.GetPressDown(SteamVR_Controller.ButtonMask.Axis0)) {
        showHints = false;
        // print("ShowHints: " + showHints);
      }

      bool a1 = hand.controller.GetPressUp(SteamVR_Controller.ButtonMask.Axis0);
      if (a1) {
        showHints = false;
      }

      if (!showing) { continue; }
      if (showing && hand != currentHand) { continue; }


      Vector2 v = hand.controller.GetAxis();

      // bool b0 = hand.controller.GetPressUp(SteamVR_Controller.ButtonMask.Axis0);


      if (v.x > 0.5 && v.y < 0.5 && v.y > -0.5) {
        //Right
        SetTouchpadIndicator(4);
        TriggerItem(currentItem, 1);
        showHints = false;
        continue;
      } else if (v.x < -0.5 && v.y < 0.5 && v.y > -0.5) {
        //Left
        SetTouchpadIndicator(3);
        TriggerItem(currentItem, -1);
        showHints = false;
        continue;
      } else if (v.y > 0.5 && v.x < 0.5 && v.x > -0.5) {
        //Up
        SetTouchpadIndicator(1);
        downCounter = 0;
        if (upCounter < changeMax) {
          upCounter++;
          continue;
        }
        showHints = false;
        changed = true;
        SetSelected(currentItem-1);
        upCounter = 0;
      } else if (v.y < -0.5 && v.x < 0.5 && v.x > -0.5) {
        //Down
        SetTouchpadIndicator(2);
        upCounter = 0;
        if (downCounter < changeMax) {
          downCounter++;
          continue;
        }
        showHints = false;
        changed = true;
        SetSelected(currentItem+1);
        downCounter = 0;
      } else {
        //Neutral
        bool a0 = hand.controller.GetPress(SteamVR_Controller.ButtonMask.Axis0);
        if (!a0) {
          SetTouchpadIndicator(0);
        } else {
          SetTouchpadIndicator(5);
        }

        if (downCounter > 0 && !changed) {
          SetSelected(currentItem+1);
        } else if (upCounter > 0 && !changed) {
          SetSelected(currentItem-1);
        }

        if (a1) {
          TriggerItem(currentItem);
        }
        upCounter = 0;
        downCounter = 0;
        changed = false;
      }
    }
  }

  public void UpdateLoadUI(string name, int queueSize) {
    if (name == "") {
      panels[6].SetActive(false);
    } else {
      panels[6].SetActive(true);
      //If name is longer than limit it is truncated so that
      //the start and last 3 chars of it are visible, as these
      //represent the model instance.
      string n1 = "";
      if (name.Length > loadingCharLimit) {
        for (int i = 0; i < loadingCharLimit - 3; i++) {
          n1 += name[i];
        }
        n1 += "..";
        for (int i = 3; i > 0; i--) {
          n1 += name[name.Length-i];
        }
      } else {
        n1 = name;
      }
      loadingText.GetComponent<Text>().text = " Loading: " + n1;
    }

    if (queueSize == 0) {
      panels[7].SetActive(false);
    } else {
      panels[7].SetActive(true);
      string txt = "" + queueSize;
      if (txt.Length > 1) {
        txt = "9+";
      }
      queueText.GetComponent<Text>().text = "In Queue: " + txt;
    }
  }

  void HandleKeyInput() {
    //Input handling


    if (Input.GetKeyUp(KeyCode.Delete) || Input.GetKeyUp(KeyCode.RightControl)) {
      //App Menu Button
      ToggleUI();
      return;
    }

    if (!showing) { return; }

    if (Input.GetKeyUp(KeyCode.Return)) {
      //Trigger Released
      TriggerItem(currentItem);
      return;
    }

    if (Input.GetKeyUp(KeyCode.RightShift)) {
      //Grip Released
      ShowPanel(1);
      return;
    }




    if (Input.GetKey(KeyCode.RightArrow)) {
      //Right
      TriggerItem(currentItem, 1);
      return;
    } else if (Input.GetKey(KeyCode.LeftArrow)) {
      //Left
      TriggerItem(currentItem, -1);
      return;
    } else if (Input.GetKeyUp(KeyCode.UpArrow)) {
      //Up
      downCounter = 0;
      if (upCounter < changeMax) {
        upCounter++;

        return;
      }

      changed = true;
      SetSelected(currentItem-1);
      upCounter = 0;
    } else if (Input.GetKeyUp(KeyCode.DownArrow)) {
      //Down
      upCounter = 0;
      if (downCounter < changeMax) {
        downCounter++;
        return;
      }
      changed = true;
      SetSelected(currentItem+1);
      downCounter = 0;
    } else {
      //Neutral
      if (downCounter > 0 && !changed) {
        SetSelected(currentItem+1);
      } else if (upCounter > 0 && !changed) {
        SetSelected(currentItem-1);
      }
      upCounter = 0;
      downCounter = 0;
      changed = false;
    }
  }

  void Update() {
    if (fixScrollbar) {
      UpdateScrollbar();
      fixScrollbar = false;
    }

    if (touchpadCounter > 0) {
      touchpadCounter--;
      if (touchpadCounter == 0) {
        SetTouchpadIndicator(0);
      }
    }

    
    for (int i = 0; i < player.hands.Length; i++) {
      if (player.hands[i] == null || player.hands[i].controller == null) { continue; }
      //Check highlight is set
      if (controllerHighlights[i] == null) {
        controllerHighlights[i] = player.hands[i].gameObject.GetComponentInChildren<ControllerHoverHighlight>();
      }
      
      if (controllerHighlights[i] == null) { continue; }
      
      
      if (heldObject[i]) {
        //Holding an object => green
        controllerHighlights[i].ShowHighlight(true);
        if (grabHint[i]) {
          ControllerButtonHints.HideTextHint(player.hands[i], Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger);
          grabHint[i] = false;
        }        
      } else if (player.hands[i].hoveringInteractable != null && player.hands[i] != currentHand) {
        if (!grabHint[i]) {
          ControllerButtonHints.ShowTextHint(player.hands[i], Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger, "Grab: " + player.hands[i].hoveringInteractable.name);
          grabHint[i] = true;
        }
        //Hovering over an object => yellow
        controllerHighlights[i].ShowHighlight(false);        
      } else {
        //Nothing happening => hide
        controllerHighlights[i].HideHighlight();        
        if (grabHint[i]) {
          ControllerButtonHints.HideTextHint(player.hands[i], Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger);
          grabHint[i] = false;
        }
      }
    }


    if (showHints && !showingHints) {
      // print("Show Hints");
      ShowHints(player.hands);
    } else if (!showHints && showingHints) {
      // print("Hide Hints");
      HideHints(player.hands);
    }

    if (keyInput) { HandleKeyInput(); }
    else { HandleControllerInput(); }

    if (loadCounter > 0) {
      loadCounter--;
    }
  }

  void TriggerItem(int idx) {
    if (currentPanel == 1) {
      actions[currentPanel][idx](idx+2);
    } else if (currentPanel == 2) {
      actions[currentPanel][idx](0);
    } else if (currentPanel >= 3) {
      // // print("Trigger " + idx + ": " + actions[currentPanel][idx]);
      actions[currentPanel][idx](idx);
    }
  }

  void TriggerItem(int idx, int amt) {
    if (currentPanel == 1) { return; }
    if (!selectables[currentPanel][idx].name.StartsWith("Slider")) { return; }
      // if (selectableTimers[idx] != 0) {
        // selectableTimers[idx]--;
        // return;
      // } else {
        // TriggerItem(idx);
        // selectableTimers[idx] = selectableTimersMax;
        // return;
      // }
    // }

    actions[currentPanel][idx](amt);
  }

  void ShowHints(Hand[] hands) {
    int count = 0;
    foreach (Hand hand in hands) {
      if (hand == null) { continue; }
      if (!hand.gameObject.activeInHierarchy) { continue; }
      if (hand.controller == null) { continue; }
      count++;
      if (heldObject[count-1]) { continue; }
      if (hand == currentHand) { continue; }
      ControllerButtonHints.ShowTextHint(hand, Valve.VR.EVRButtonId.k_EButton_ApplicationMenu, "Menu");
      ControllerButtonHints.ShowTextHint(hand, Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger, "Grab");
      ControllerButtonHints.ShowTextHint(hand, Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad, "Teleport");
      ControllerButtonHints.ShowTextHint(hand, Valve.VR.EVRButtonId.k_EButton_Grip, "Hints");
      if (!string.IsNullOrEmpty( ControllerButtonHints.GetActiveHintText( hand, Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad ))) {
        showingHints = true;
      }
    }
  }

  void HideHints(Hand[] hands) {
    // if (!showingHints) { return; }
    foreach (Hand hand in hands) {
      if (hand == null) { continue; }
      ControllerButtonHints.HideTextHint(hand, Valve.VR.EVRButtonId.k_EButton_ApplicationMenu);
      ControllerButtonHints.HideTextHint(hand, Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger);
      ControllerButtonHints.HideTextHint(hand, Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad);
      ControllerButtonHints.HideTextHint(hand, Valve.VR.EVRButtonId.k_EButton_Grip);
    }
    showingHints = false;
  }




  public Hand GetCurrentHand() { return currentHand; }
  public bool IsShowing() { return showing; }

  //Toggles visibility of UI and updates lists of interactable elements
  public void ToggleUI(Hand hand = null) {
    // print("ToggleUI: " + hand);
    if (!showing && hand) {
      //Place on correct hand
      int handID = 0;
      if (!hands.Contains(hand)) {
        hands.Add(hand);
        anchors.Add(hand.GetComponent<UIAnchor>().anchor);
        handID = hands.Count-1;
      } else {
        for (int i = 0; i < hands.Count; i++) {
          if (hands[i] == hand) {
            handID = i;
          }
        }
      }
      currentHand = hands[handID];
      panels[0].transform.position = anchors[handID].position;
      panels[0].transform.SetParent(anchors[handID]);
      panels[0].transform.rotation = anchors[handID].rotation;
      // menuIndicator.transform.position = currentHand.transform.position + new Vector3(0,0.005f,-0.0498f);
      menuIndicator.transform.parent = currentHand.transform;
      menuIndicator.transform.localPosition = new Vector3(0,0.005f,-0.0498f);

      menuIndicator.transform.rotation = menuIndicator.transform.parent.rotation;
      menuIndicator.transform.Rotate(new Vector3(83.25f,0,0));
      menuIndicator.transform.localScale = new Vector3(0.04f,0.04f,1);


    }

    showing = !showing;
    menuIndicator.SetActive(showing);
    // if (showing) {
    SetTouchpadIndicator(0);
    // }
    // touchpadSelection = 0;
    panels[currentPanel].SetActive(showing);
    panels[5].SetActive(showing);
    GetSelectablesAndActions(currentPanel);




    //Force UI selection update by switching selection twice (once isn't enough)
    SetSelected(1);
    SetSelected(0);
    // showingHints = false;
  }

  void SetTouchpadIndicator(int idx) {
    if (touchpadSelection == idx) { return; }
    for (int i = 0; i < menuIndicator.transform.childCount; i++) {
      if (i != idx) {
        menuIndicator.transform.GetChild(i).gameObject.SetActive(false);
      } else {
        menuIndicator.transform.GetChild(i).gameObject.SetActive(true);
      }
    }
    touchpadSelection = idx;
    touchpadCounter = touchpadCounterMax;
  }

  public void ShowPanel(int i) {
    if (i == 0) { ToggleUI(); return; }
    if (currentPanel == i) { return; }

    if (colourChanged) {
      DisableHighlight(currentItem);
    }

    panels[currentPanel].SetActive(false);
    currentPanel = i;
    panels[currentPanel].SetActive(true);

    currentItem = 0;

    if (currentPanel == 3 || currentPanel == 4 || !selectables.ContainsKey(currentPanel)) {
      GetSelectablesAndActions(currentPanel);
    } else {
      selectableTimers = new int[selectables[currentPanel].Count];
    }

    SetSelected(currentItem);
    // // print("Selected: " + eventSystem.currentSelectedGameObject);
  }

  void SetupUI() {
    CreateVisPanels();
    ToggleVisPanel(-1);
    CreateControlPanels();
    ToggleControlPanel(-1);
  }

  void DisableHighlight(int idx) {
    GameObject g = selectables[currentPanel][currentItem];
    if (g.name.StartsWith("Panel")) {
      g.GetComponent<Image>().color = lastColour;
      colourChanged = false;
    }
  }

  void SetSelected(int idx) {
    if (idx < 0) { idx = 0; }
    if (idx >= selectables[currentPanel].Count) { idx = selectables[currentPanel].Count-1; }

    if (colourChanged) {
      DisableHighlight(currentItem);
    }

    currentItem = idx;
    GameObject g = selectables[currentPanel][currentItem];
    eventSystem.SetSelectedGameObject(g);

    if (g.name.StartsWith("Panel")) {
      Image i = g.GetComponent<Image>();
      lastColour = i.color;
      i.color = highlight;
      colourChanged = true;
    }

    fixScrollbar = true;
  }

  void UpdateScrollbar() {
    //It works, but functionality may seem a bit odd
    if (currentPanel == 3 || currentPanel == 4) {
      int i = currentPanel-3;
      GameObject g = eventSystem.currentSelectedGameObject;
      Vector2 offset = Vector2.zero;

      //Get parent Block
      if (g.transform.parent.gameObject.name != "Content") {
        if (currentPanel == 3) {
          g = g.transform.parent.parent.gameObject;
        } else {
          g = g.transform.parent.gameObject;
        }
        offset += g.transform.parent.GetComponent<RectTransform>().offsetMax;
      }
      // print("G: " + g.name);
      RectTransform rt = g.GetComponent<RectTransform>();
      float selectedMax = -(rt.offsetMin.y + offset.y) + 5;
      float selectedHeight = rt.offsetMin.y - rt.offsetMax.y;
      float selectedMin = selectedMax + selectedHeight - 5;
      float conSize = rta[i].offsetMax.y - rta[i].offsetMin.y;
      float conMax = conSize - contentSize;
      float currentPos = (1 - sba[i].value) * conMax;
      // print("---------------------------------");
      // print("RTA 0: " + rta[i].offsetMin.y + "," + rta[i].offsetMax.y);
      // print("Selected: " + selectedMin + "," + selectedMax + " | " + selectedHeight);
      // print("CurPos: " + (conSize) + " | " + currentPos);
      // print("ConSize | ConMax | CurPos: " + conSize + " | " + conMax + " | " + currentPos);

      // print("Bool: " + (selectedMin < currentPos) + " " + (selectedMax > (contentSize + currentPos) && (Mathf.Abs(selectedHeight) < contentSize)));

      if (selectedMin < currentPos) {
        sba[i].value = (conSize - selectedMin) / conSize;
      } else if (selectedMax > (contentSize + currentPos) && (Mathf.Abs(selectedHeight) < contentSize)) {
        sba[i].value = 1 - ((selectedMax - contentSize) / conMax);
      }
    }
  }

  //Adjust current slider by amt
  void AdjustSlider(int amt) {
    // print("AdjustSlider " + currentItem + " " + amt);
    Slider s = selectables[currentPanel][currentItem].GetComponent<Slider>();

    if (amt == 0) {
      s.value = 0;
    } else {
      s.value += amt;
    }

    // if (amt) {

    if (currentPanel == 2) {
      RotationSliders(currentItem,amt);
    } else if (currentPanel == 3) {
      VisibilitySliders(currentItem,s.value);
    }
  }

  void RotationSliders(int idx, int amt) {
    if (idx == 0) {
      if (amt == 0) {
        rc.ResetGantry();
      } else {
        rc.RotateGantry(amt);
      }
    } else if (idx > 0 && idx <= 3) {
      if (amt == 0) {
        rc.ResetBedRotation(idx-1);
      } else {
        rc.RotateBed(idx-1,amt);
      }
    } else if (idx > 3 && idx <= 6) {
      if (amt == 0) {
        rc.ResetBedMovement(idx-4);
      } else {
        rc.MoveBed(idx-4,amt);
      }
    }
  }

  /** ================================================================================
    Change visibility of individual body parts by
    adjusting the alpha value of the shared material.

    Some Issues:
    - Using the standard shader doesn't work as changing transparency at run time
      doesn't update the models, even though colour does. The problem is that
      changing the mode to fade doesn't work at runtime.
    - Creating a material using the standard shader already set to fade allows for
      adjustable transparency at runtime, but using a prefab means this material
      is shared between all objects, so all body parts are the same colour + alpha.
    - This could be solved by creating many different materials in the editor, but
      each new model loaded needs a new set of materials. E.g. Lung set is 9 per model.

    Solution:
    Use legacy transparency shader.

  =================================================================================**/
  void VisibilitySliders(int idx, float amt) {
    amt = (amt / 255);
    //Adjust idx based on expanded panels
    int idx2 = 2;
    for (int i = 0; i < visControls.Count; i++) {
      idx2++;
      if (visControls[i].open) {
        if (idx2 + visControls[i].mats.Count >= idx) {
          Material m = visControls[i].mats[idx - idx2];
          m.color = new Color(m.color.r,m.color.g,m.color.b,amt);
          return;
        }
        idx2+= visControls[i].mats.Count;
      }
    }
  }

  //Toggle Lasers
  void ToggleLasers() {
    lc.ToggleLasers();
    if (laserText.text.EndsWith("F")) {
      laserText.text = laserText.text.Replace("OFF","ON");
    } else {
      laserText.text = laserText.text.Replace("ON","OFF");
    }
  }

  //Toggle X-ray particle effect
  void ToggleBeam() {
    if (allowXray) {
      xc.Toggle();
      if (xrayText.text.EndsWith("F")) {
        xrayText.text = xrayText.text.Replace("OFF","ON");
      } else {
        xrayText.text = xrayText.text.Replace("ON","OFF");
      }
    }
  }

  void ChangePanel(int close) {
    // print("ChangePanel " + currentPanel + " " + currentItem + " " + close);
    if (currentPanel == 3) {
      int idx2 = 2;
      for (int i = 0; i < visControls.Count; i++) {
        if (idx2 == currentItem) {
          ToggleVisPanel(i);
          // VisControl v = visControls[i];
          // bool c = v.open;
          // v.open = (close == 1 ? false : true);
          // // print("VC " + i + " Open: " + c + " -> " + v.open);
          break;
        }
        idx2++;
        idx2 += (visControls[i].open ? visControls[i].mats.Count : 0);
      }
    } else if (currentPanel == 4) {
      int idx2 = 0;
      for (int i = 0; i < modControls.Length; i++) {
        if (idx2 == currentItem) {
          // // print("Toggle Panel: " + i);
          ToggleControlPanel(i);
          // bool c = modControls[i].open;
          // modControls[i].open = (close == 1 ? false : true);
          // // print("MC " + i + " Open: " + c + " -> " + modControls[i].open);
          break;
        }
        idx2++;
        if (modControls[i].open) {
          if (i == 0) {
            idx2 += modControls[i].files.Count;
          } else {
            //Or break here
            idx2 += modControls[i].models.Count;
          }
        }
      }
    }
    GetSelectablesAndActions(currentPanel);
    fixScrollbar = true;
  }

  void LoadModel(int idx) {
    // if (loadCounter == 0) {
    fr.LoadModel(modControls[0].files[idx-1]);
      // print("Loading Model: " + modControls[0].files[idx-1]);
      // loadCounter = loadDelay;
    // }
  }

  void RemoveModel(int idx) {
    if (idx == -1) {
      fr.RemoveAllModels();
    } else {
      //Find idx offset

      int idx2 = 2;
      if (modControls[0].open) {
        idx2 += modControls[0].files.Count;
      }
      // print("RemoveModels: " + idx + "," + idx2);
      fr.RemoveModel(idx-idx2);
    }
    UpdateUI();
  }

  //Generate selectable objects and related actions (methods) for UI objects
  void GetSelectablesAndActions(int idx) {
    //Top Menu
    if (idx == 1) {
      List<GameObject> g = new List<GameObject>();
      List<Action<int>> acts = new List<Action<int>>();
      Action<int> a;
      for (int i = 0; i < panels[1].transform.childCount; i++) {
        g.Add(panels[1].transform.GetChild(i).gameObject);
        if (i < panels[1].transform.childCount-1) {
          a = n => ShowPanel(n);
          acts.Add(a);
        } else {
          a = n => ToggleUI();
          acts.Add(a);
        }
      }
      selectables[1] = g;
      actions[1] = acts;
    }

    //Rotation Menu
    if (idx == 2) {
      List<GameObject> g = new List<GameObject>();
      List<Action<int>> acts = new List<Action<int>>();

      //Gantry Slider
      g.Add(panels[2].transform.GetChild(1).GetChild(0).GetChild(1).GetChild(0).GetChild(1).gameObject);
      Action<int> a = n => AdjustSlider(n);
      acts.Add(a);

      //Bed Sliders (6)
      Transform top = panels[2].transform.GetChild(1).GetChild(1);
      for (int i = 1; i < top.childCount; i++) {
        g.Add(top.GetChild(i).GetChild(0).GetChild(1).gameObject);
        a = n => AdjustSlider(n);
        acts.Add(a);
      }

      selectables[2] = g;
      actions[2] = acts;
    }

    //Vis Menu - Dynamic
    if (idx == 3) {
      //2 Buttons
      List<GameObject> g = new List<GameObject>();
      List<Action<int>> acts = new List<Action<int>>();
      Action<int> a;
      //1, 0, 0, 0 - Laser Button
      //1, 0, 0, 1 - Beam Button
      g.Add(panels[3].transform.GetChild(1).GetChild(0).GetChild(0).GetChild(0).gameObject);
      acts.Add(n => ToggleLasers());
      g.Add(panels[3].transform.GetChild(1).GetChild(0).GetChild(0).GetChild(1).gameObject);
      acts.Add(n => ToggleBeam());

      //Some number of panels with some number of children each
      Transform top = panels[3].transform.GetChild(1).GetChild(0).GetChild(0);
      for (int i = 2; i < top.childCount; i++) {
        g.Add(top.GetChild(i).gameObject);
        //Add children if panel is expanded
        if (visControls[i - 2].open) {
          acts.Add(n => ChangePanel(1));
          for (int j = 1; j < top.GetChild(i).childCount; j++) {
            //i, j, 0, 1
            g.Add(top.GetChild(i).GetChild(j).GetChild(0).GetChild(1).gameObject);
            acts.Add(n => AdjustSlider(n));
          }
        } else {
          acts.Add(n => ChangePanel(0));
        }
      }
      selectables[3] = g;
      actions[3] = acts;
    }

    //Load Menu - Dynamic
    if (idx == 4) {
      //Two panels (Load, Remove) with some number of children
      List<GameObject> g = new List<GameObject>();
      List<Action<int>> acts = new List<Action<int>>();
      Transform top = panels[4].transform.GetChild(1).GetChild(0).GetChild(0);

      //Load Panel
      g.Add(top.GetChild(0).gameObject);

      //Add children
      if (modControls[0].open) {
        acts.Add(n => ChangePanel(1));
        for (int i = 1; i < top.GetChild(0).childCount; i++) {
          g.Add(top.GetChild(0).GetChild(i).GetChild(0).gameObject);
          acts.Add(n => LoadModel(n));
        }
      } else {
        acts.Add(n => ChangePanel(0));
      }

      //Remove Panel
      g.Add(top.GetChild(1).gameObject);
      if (modControls[1].open) {
        acts.Add(n => ChangePanel(1));
        for (int i = 1; i < top.GetChild(1).childCount; i++) {
          g.Add(top.GetChild(1).GetChild(i).GetChild(0).gameObject);
          acts.Add(n => RemoveModel(n));
        }
      } else {
        acts.Add(n => ChangePanel(0));
      }

      //1 Button - 2, 0, 0
      g.Add(top.GetChild(2).GetChild(0).GetChild(0).gameObject);
      acts.Add(n => RemoveModel(-1));
      selectables[4] = g;
      actions[4] = acts;
    }

    selectableTimers = new int[selectables[currentPanel].Count];

    foreach (GameObject g in selectables[idx]) {
      if (g.GetComponent<Slider>() && !g.name.StartsWith("Slider")) {
        g.name = "Slider " + g.name;
      }
    }
  }



  void CreateVisPanels() {
    //Creates UI elements for model visibility
    Transform content = panels[3].transform.GetChild(1).GetChild(0).GetChild(0);
    float yGap = 5;
    float yMin = content.GetChild(content.childCount-1).GetComponent<RectTransform>().offsetMin.y - yGap;

    visControls.Clear();
    List<MeshMaker.Model> models = fr.GetModels();
    GameObject panel = Resources.Load("Prefab/Visibility Panel Large") as GameObject;
    GameObject block = Resources.Load("Prefab/Visibility Model Block") as GameObject;
    // print("CreateVisPanels: " + models.Count);
    foreach (MeshMaker.Model m in models) {
      VisControl vc = new VisControl();
      vc.models = m.models;
      vc.mats = m.mats;
      GameObject p = CreateGameObject(panel,content);
      vc.text = p.transform.Find("Model Name").GetComponent<Text>();
      vc.text.text = " > " + m.name;
      p.name = "Panel: " + m.name;

      //Base height = 20 normal, 40 expanded (1 item)
      float height = 20;
      float bStart = -18;
      float bHeight = 24;
      for (int i = 0; i < vc.mats.Count; i++) {
        GameObject b = CreateGameObject(block,p.transform);
        b.transform.GetChild(0).GetChild(0).GetComponent<Text>().text = (i+1) + "";
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(0,bStart-bHeight);
        rt.offsetMax = new Vector2(0,bStart);
        bStart -= bHeight;
        height += bHeight;
        b.SetActive(false);
      }
      vc.minHeight = 20;
      vc.maxHeight = height;
      vc.open = false;
      vc.rt = p.GetComponent<RectTransform>();
      visControls.Add(vc);
    }
  }



  void ToggleVisPanel(int idx) {
    Transform content = panels[3].transform.GetChild(1).GetChild(0).GetChild(0);
    float yGap = 5;
    float yMin = content.GetChild(1).GetComponent<RectTransform>().offsetMin.y;
    RectTransform rt = content.GetComponent<RectTransform>();
    // // print("ToggleVisPanel yMin: " + yMin + " | " + rt.offsetMin + " / " + rt.offsetMax);

    for (int i = 0; i < visControls.Count; i++) {
      VisControl vc = visControls[i];
      yMin -= yGap;
      vc.rt.offsetMax = new Vector2(0,yMin);
      if (i == idx) {
        if (vc.open) {
          vc.text.text = vc.text.text.Replace('v','>');
        } else {
          vc.text.text = vc.text.text.Replace('>','v');
        }
        vc.open = !vc.open;
        // // print("Open: " + open + ", J: " + content.childCount + ", K: " + content.GetChild(2).
        // for (int j = 2; j < content.childCount; j++) {
          for (int k = 1; k < content.GetChild(i+2).childCount; k++) {
            content.GetChild(i+2).GetChild(k).gameObject.SetActive(vc.open);
          }
        // }

      }
      yMin -= (vc.open ? vc.maxHeight : vc.minHeight);
      vc.rt.offsetMin = new Vector2(0,yMin);
      // Scrollbar sb = content.parent.parent.GetChild(1).GetComponent<Scrollbar>();
      // sb.value = 1;
      // // print("SB: " + sb.value);
    }

    float conSize = rta[0].offsetMax.y - rta[0].offsetMin.y;
    rta[0].offsetMin = new Vector2(0,0);
    rta[0].offsetMax = new Vector2(0,Mathf.Max(contentSize,-yMin));
    // if (-yMin > contentSize) {
      // rt.offsetMax = new Vector2(0,-yMin);
    // }

  }

  void RemoveVisPanels() {
    Transform content = panels[3].transform.GetChild(1).GetChild(0).GetChild(0);
    while (content.childCount > 2) {
      Transform t = content.GetChild(content.childCount-1);
      t.parent = null;
      Destroy (t.gameObject);
    }
  }

  void RemoveControlPanels() {
    Transform content = panels[4].transform.GetChild(1).GetChild(0).GetChild(0);
    while (content.childCount > 1) {
      Transform t = content.GetChild(0);
      t.parent = null;
      Destroy (t.gameObject);
    }
  }

  void CreateControlPanels() {
    //Creates UI elements for model control (loading / removing)

    //Starting position for panels
    Transform content = panels[4].transform.GetChild(1).GetChild(0).GetChild(0);
    float yGap = 5;
    float yMin = -15;

    modControls = new ModControl[2];
    GameObject panel = Resources.Load("Prefab/Model Control Panel") as GameObject;
    GameObject block = Resources.Load("Prefab/Model Control Block") as GameObject;

    //Find loadable folders
    List<string> files = fr.FindLoadableFolders();
    //Find currently loaded models
    List<MeshMaker.Model> models = fr.GetModels();

    //Setup Load Panel
    ModControl mc = new ModControl();
    mc.files = files;
    mc.open = false;
    mc.minHeight = 20;
    GameObject p = CreateGameObject(panel,content);
    p.name = "Panel: Load Model";
    mc.text = p.transform.Find("Title").GetComponent<Text>();
    mc.text.text = " > Load Model";
    mc.rt = p.GetComponent<RectTransform>();
    p.transform.SetSiblingIndex(0);

    float height = 20;
    float bStart = -18;
    float bHeight = 15;

    for (int i = 0; i < files.Count; i++) {
      GameObject b = CreateGameObject(block,p.transform);
      string[] split = files[i].Split('\\');
      b.GetComponentsInChildren<Text>()[0].text = split[split.Length-1];
      RectTransform rt = b.GetComponent<RectTransform>();
      rt.offsetMin = new Vector2(0,bStart-bHeight);
      rt.offsetMax = new Vector2(0,bStart);
      bStart -= bHeight;
      height += bHeight;
      b.SetActive(false);
    }
    mc.maxHeight = height;
    modControls[0] = mc;

    //Setup Remove Panel
    mc = new ModControl();
    List<GameObject> gos = new List<GameObject>();
    foreach (MeshMaker.Model m in models) {
      gos.Add(m.top);
    }
    mc.models = gos;
    mc.open = false;
    mc.minHeight = 20;
    p = CreateGameObject(panel,content);
    p.name = "Panel: Remove Model";
    mc.text = p.transform.Find("Title").GetComponent<Text>();
    mc.text.text = " > Remove Model";
    mc.rt = p.GetComponent<RectTransform>();
    p.transform.SetSiblingIndex(1);
    height = 20;
    bStart = -18;

    for (int i = 0; i < gos.Count; i++) {
      GameObject b = CreateGameObject(block,p.transform);
      b.GetComponentsInChildren<Text>()[0].text = gos[i].name;
      RectTransform rt = b.GetComponent<RectTransform>();
      rt.offsetMin = new Vector2(0,bStart-bHeight);
      rt.offsetMax = new Vector2(0,bStart);
      bStart -= bHeight;
      height += bHeight;
      b.SetActive(false);
    }
    mc.maxHeight = height;
    modControls[1] = mc;
  }

  void ToggleControlPanel(int idx) {
    Transform content = panels[4].transform.GetChild(1).GetChild(0).GetChild(0);
    float yGap = 5;
    float yMin = -10;
    // // print(modControls.Length);
    // // print(modControls[0] + "," + modControls[0].rt);
    // // print(modControls[1] + "," + modControls[1].rt);

    RectTransform rt = null;
    for (int i = 0; i < modControls.Length; i++) {
      ModControl mc = modControls[i];
      mc.rt.offsetMax = new Vector2(0,yMin);
      if (i == idx) {
        bool b = mc.open;
        if (mc.open) {
          // mc.text.text = mc.text.text.Replace('v','>');
          mc.text.text = " > " + mc.text.text.TrimStart(new char[]{' ','v'});
        } else {
          mc.text.text = mc.text.text.Replace('>','v');

        }
        mc.open = !mc.open;
        // modControls[i] = mc;

        for (int j = 1; j < content.GetChild(i).childCount; j++) {
          content.GetChild(i).GetChild(j).gameObject.SetActive(mc.open);
        }
        // // print("ToggleControlPanel: " + idx + " " + b + " => " + mc.open);
      }
      yMin -= (mc.open ? mc.maxHeight : mc.minHeight);
      mc.rt.offsetMin = new Vector2(0,yMin);
      yMin -= yGap;
    }

    //Remove All Button
    // // print("C: " + content.childCount);
    rt = content.GetChild(2).GetComponent<RectTransform>();
    rt.offsetMax = new Vector2(0,yMin);
    yMin -= 18;
    rt.offsetMin = new Vector2(0,yMin);
    yMin -= yGap;

    float conSize = rta[1].offsetMax.y - rta[1].offsetMin.y;
    rta[1].offsetMin = new Vector2(0,0);
    rta[1].offsetMax = new Vector2(0,Mathf.Max(contentSize,-yMin));
    // if (-yMin > contentSize) {
      // content.GetComponent<RectTransform>().offsetMax = new Vector2(0,-yMin);
    // }
  }



  GameObject CreateGameObject(GameObject target, Transform parent) {
    GameObject g = Instantiate(target) as GameObject;
    // g.transform.parent = parent;
    g.transform.SetParent(parent);
    g.transform.localScale = new Vector3(1,1,1);
    g.transform.localPosition = new Vector3(g.transform.localPosition.x, g.transform.localPosition.y,0);
    g.transform.localRotation = Quaternion.identity;

    return g;
  }

}