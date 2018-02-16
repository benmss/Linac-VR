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
         i. Y axis rotation (90° max?)
        ii. X axis rotation ( 3° max?)
       iii. Z axis rotation (max??)
        iv. Up/Down Movement
         v. Left/Right Movement
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

  List<VisControl> visControls = new List<VisControl>();
  ModControl[] modControls = new ModControl[2];
  static Player player;
  int currentPanel = 1;
  int currentItem = 0;
  Dictionary<int,List<GameObject>> selectables = new Dictionary<int,List<GameObject>>();
  Dictionary<int,List<Action<int>>> actions = new Dictionary<int,List<Action<int>>>();

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
  float contentSize = 217.5f;

  Scrollbar[] sba = new Scrollbar[2];
  RectTransform[] rta = new RectTransform[2];
  Text laserText;
  Text xrayText;
  

  bool fixScrollbar = false;

  public GameObject[] panels;
  public EventSystem eventSystem;
  public bool keyInput = true;
  public GameObject laserButton;
  public GameObject xrayButton;


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


    fr = GameObject.Find("File Controller").GetComponent<FileReader>();
    rc = GameObject.Find("Rotation Controller").GetComponent<RotationController>();
    lc = GameObject.Find("Laser Controller").GetComponent<LaserController>();
    xc = GameObject.Find("X-ray Controller").GetComponent<XrayController>();
    // panels = gameObject.GetComponent<UIController>().panels;
    if (panels.Length == 0) { return; }



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
    if (!player || player.hands == null) { return; }
    foreach ( Hand hand in player.hands ) {
      if (hand.startingHandType == Hand.HandType.Left) {
        if (!hand) { continue; }
        if (hand.controller == null) { continue; }
        if (hand.controller.GetPressUp(SteamVR_Controller.ButtonMask.ApplicationMenu)) {
          //App Menu Button
          ToggleUI();
          return;
        }

        if (!showing) { continue; }

        if (hand.controller.GetHairTriggerUp()) {
          //Trigger Released
          TriggerItem(currentItem);
          return;
        }

        if (hand.controller.GetPressUp(SteamVR_Controller.ButtonMask.Grip)) {
          //Grip Released
          ShowPanel(1);
          return;
        }

        Vector2 v = hand.controller.GetAxis();

        if (v.x > 0.5 && v.y < 0.5 && v.y > -0.5) {
          //Right
          TriggerItem(currentItem, 1);
          return;
        } else if (v.x < -0.5 && v.y < 0.5 && v.y > -0.5) {
          //Left
          TriggerItem(currentItem, -1);
        } else if (v.y > 0.5 && v.x < 0.5 && v.x > -0.5) {
          //Up
          downCounter = 0;
          if (upCounter < changeMax) {
            upCounter++;

            return;
          }

          changed = true;
          SetSelected(currentItem-1);
          upCounter = 0;
        } else if (v.y < -0.5 && v.x < 0.5 && v.x > -0.5) {
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
    //Debug code
    if (currentPanel == 3) {
      // Scrollbar sb = panels[3].transform.GetChild(1).GetChild(1).GetComponent<Scrollbar>();
      // // print("Sb: " + sb.value);
      // if (sb.value != 1) { sb.value = 1; }
    }

    if (fixScrollbar) {
      UpdateScrollbar();
      fixScrollbar = false;
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
    } else if (currentPanel >= 2) {
      // // print("Trigger " + idx + ": " + actions[currentPanel][idx]);
      actions[currentPanel][idx](idx);
    }
  }

  void TriggerItem(int idx, int amt) {
    actions[currentPanel][idx](amt);
  }

  public void ToggleUI() {
    showing = !showing;
    panels[0].SetActive(showing);
    panels[currentPanel].SetActive(showing);
    panels[5].SetActive(showing);
    GetSelectablesAndActions(currentPanel);

    //Force UI selection update by switching selection twice (once isn't enough)
    SetSelected(1);
    SetSelected(0);
  }

  public void ShowPanel(int i) {
    // // print("Show Panel: " + i);
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
      if (currentPanel == 3) {

      }
      GetSelectablesAndActions(currentPanel);
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
    s.value += amt;

    if (currentPanel == 2) {
      RotationSliders(currentItem,amt);
    } else if (currentPanel == 3) {
      VisibilitySliders(currentItem,s.value);
    }
  }

  void RotationSliders(int idx, int amt) {
    if (idx == 0) {
      //Gantry X Axis

    } else if (idx == 1) {
      //Bed X Axis

    } else if (idx == 2) {
      //Bed Y Axis

    } else if (idx == 3) {
      //Bed Z Axis

    } else if (idx == 4) {
      //Bed Move LR

    } else if (idx == 5) {
      //Bed Move UD

    } else if (idx == 6) {
      //Bed Move FB
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
    lc.Toggle();
    if (laserText.text.EndsWith("F")) {
      laserText.text = laserText.text.Replace("OFF","ON");
    } else {
      laserText.text = laserText.text.Replace("ON","OFF");
    }
  }

  //Toggle X-ray particle effect
  void ToggleBeam() {
    xc.Toggle();
    if (xrayText.text.EndsWith("F")) {
      xrayText.text = xrayText.text.Replace("OFF","ON");
    } else {
      xrayText.text = xrayText.text.Replace("ON","OFF");
    }
  }

  void ChangePanel(int close) {
    print("ChangePanel " + currentPanel + " " + currentItem + " " + close);
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
    if (loadCounter == 0) {
      fr.LoadModel(modControls[0].files[idx-1]);
      // print("Loading Model: " + modControls[0].files[idx-1]);
      loadCounter = loadDelay;
    }
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
    g.transform.parent = parent;
    g.transform.localScale = new Vector3(1,1,1);
    g.transform.localPosition = new Vector3(g.transform.localPosition.x, g.transform.localPosition.y,0);
    g.transform.localRotation = Quaternion.identity;

    return g;
  }

}