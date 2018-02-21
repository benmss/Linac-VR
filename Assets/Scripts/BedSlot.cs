using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BedSlot : MonoBehaviour {
  
  HashSet<GameObject> currentModels = new HashSet<GameObject>();
  bool full = false;
  

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
  
  //Perform fullness check when removing an object from UI
  public void CheckRemove(GameObject g) {
    if (currentModels.Contains(g)) {
      currentModels.Remove(g);
      FullCheck();
      print("BedSlot removed: " + g.name);
    }
  }
  
  //Check state of fullness
  void FullCheck() {
    if (!full && currentModels.Count > 0) {
      full = true;
    } else if (full && currentModels.Count == 0) {
      full = false;
    }
  }
  
  public bool IsFull() { return full; }
  
  void OnTriggerEnter(Collider other) {
    if (!currentModels.Contains(other.gameObject)) {
      currentModels.Add(other.gameObject);
    }
    FullCheck();
  }
  
  void OnTriggerExit(Collider other) {
    if (!currentModels.Contains(other.gameObject)) {
      currentModels.Remove(other.gameObject);
    }
    FullCheck();
  }
}
