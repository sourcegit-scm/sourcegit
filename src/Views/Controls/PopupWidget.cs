using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     可显示弹出面板的容器接口
    /// </summary>
    public interface IPopupContainer {
        void Show(PopupWidget widget);
        void ShowAndStart(PopupWidget widget);
        void UpdateProgress(string message);
    }

    /// <summary>
    ///     可弹出面板
    /// </summary>
    public class PopupWidget : UserControl {
        private static Dictionary<string, IPopupContainer> containers = new Dictionary<string, IPopupContainer>();
        private static string currentContainer = null;
        private IPopupContainer mine = null;

        /// <summary>
        ///     注册一个弹出容器
        /// </summary>
        /// <param name="id">页面ID</param>
        /// <param name="container">容器实例</param>
        public static void RegisterContainer(string id, IPopupContainer container) {
            if (containers.ContainsKey(id)) containers[id] = container;
            else containers.Add(id, container);
        }

        /// <summary>
        ///     删除一个弹出容器
        /// </summary>
        /// <param name="id">容器ID</param>
        public static void UnregisterContainer(string id) {
            if (containers.ContainsKey(id)) containers.Remove(id);
        }

        /// <summary>
        ///     设置当前的弹出容器
        /// </summary>
        /// <param name="id">容器ID</param>
        public static void SetCurrentContainer(string id) {
            currentContainer = id;
        }

        /// <summary>
        ///     显示
        /// </summary>
        public void Show() {
            if (string.IsNullOrEmpty(currentContainer) || !containers.ContainsKey(currentContainer)) return;
            mine = containers[currentContainer];
            mine.Show(this);
        }

        /// <summary>
        ///     显示并直接点击开始
        /// </summary>
        public void ShowAndStart() {
            if (string.IsNullOrEmpty(currentContainer) || !containers.ContainsKey(currentContainer)) return;
            mine = containers[currentContainer];
            mine.ShowAndStart(this);
        }

        /// <summary>
        ///     窗体标题
        /// </summary>
        /// <returns>返回具体的标题</returns>
        public virtual string GetTitle() {
            return "TITLE";
        }

        /// <summary>
        ///     点击确定时的回调，由程序自己
        /// </summary>
        /// <returns>返回一个任务，任务预期返回类型为bool，表示是否关闭Popup</returns>
        public virtual Task<bool> Start() {
            return null;
        }

        /// <summary>
        ///     更新进度显示
        /// </summary>
        /// <param name="message"></param>
        protected void UpdateProgress(string message) {
            mine?.UpdateProgress(message);
        }
    }
}
