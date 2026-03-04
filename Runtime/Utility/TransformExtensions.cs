using UnityEngine;
 using System.Collections;
 
 public static class ExtensionMethods
 {
     private static Transform _findInChildren(Transform trans, string name)
     {
         if(trans.name == name)
             return trans;
         else
         {
             Transform found;
 
             for(int i = 0; i < trans.childCount; i++)
             {                
                 found = _findInChildren(trans.GetChild(i), name);
                 if(found != null)
                     return found;
             }
 
             return null;
         }
     }
 
     public static Transform FindInChildren(this Transform trans, string name)
     {
         return _findInChildren(trans, name);
     }
 }
