using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepter
{
    public class ProjectTreeAsset
    {
        public string Name { get; set; }
    }

    public class ProjectTree
    {

        public ObservableCollection<ProjectTreeAsset> Sounds { get; set; } = new ObservableCollection<ProjectTreeAsset>(new List<ProjectTreeAsset>()
        {
            new ProjectTreeAsset(){ Name = "test" }
        });
    }
}
