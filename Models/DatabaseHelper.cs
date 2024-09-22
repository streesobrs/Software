using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Software.Models
{
    public static class DatabaseHelper
    {
        public static string GetDatabasePath()
        {
            // 获取用户文档目录的路径
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // 在文档目录下创建一个名为StreeDB的文件夹
            string folderPath = Path.Combine(documentsPath, "StreeDB");

            // 如果文件夹不存在，则创建它
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 根据编译条件（Debug或Release）设置数据库文件的名称
            #if DEBUG
                return Path.Combine(folderPath, "debug_SoftwareDatabase.db");
            #else
                return Path.Combine(folderPath, "SoftwareDatabase.db");
            #endif
        }
    }
}
