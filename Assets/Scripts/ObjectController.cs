using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectController : MonoBehaviour {
  
  HashSet<GameObject> currentModels = new HashSet<GameObject>();
  bool full = false;
  GameObject currentModel;
  
  //Some Object with a collider that represents the target area
  public GameObject constraintArea;
  
  Bounds constraintBounds;
  

	// Use this for initialization
	void Start () {
		constraintBounds = constraintArea.GetComponent<Collider>().bounds;
	}
	
	// Update is called once per frame
	void Update () {
		
	}
  
  //Perform fullness check when removing an object from UI
  public void CheckRemove(GameObject g) {
    // if (currentModels.Contains(g)) {
      // currentModels.Remove(g);
      // FullCheck();
      // print("BedSlot removed: " + g.name);
    // }
    
    if (currentModel == g) {
      currentModel = null;
      full = false;
    }
  }
  
  
  
  public bool AddObject(GameObject g) {
    if (full) { return false; }
    currentModel = g;
    full = true;
    return true;
  }
  
  //Check state of fullness
  void FullCheck() {
    if (!full && currentModels.Count > 0) {
      full = true;
    } else if (full && currentModels.Count == 0) {
      full = false;
    }
  }
  
  void EnterCheck() {
    if (!full && currentModel) {
      full = true;
    }
  }
  
  void ExitCheck() {
    if (full && !currentModel) {
      full = false;
    }
  }
  
  public bool IsFull() { return full; }
  
  void OnTriggerEnter(Collider other) {
    // if (!currentModels.Contains(other.gameObject)) {
      // currentModels.Add(other.gameObject);
    // }
    // FullCheck();
    
    if (!currentModel) {
      currentModel = other.gameObject;
    }
  }
  
  void OnTriggerExit(Collider other) {
    // if (!currentModels.Contains(other.gameObject)) {
      // currentModels.Remove(other.gameObject);
    // }
    // FullCheck();
    
    if (currentModel && other.gameObject == currentModel) {
      currentModel = other.gameObject;
    }
  }
}
