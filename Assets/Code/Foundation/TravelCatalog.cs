using System.Collections.Generic;
using UnityEngine;
namespace TravelApp
{
    public sealed class TravelCatalog : ScriptableObject
    {
        public List<Product> products = new List<Product>();
        public List<User> users = new List<User>();
    }
}
