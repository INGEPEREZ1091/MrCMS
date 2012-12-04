using System.Collections.Generic;
using System.Linq;
using MrCMS.Entities.Documents.Layout;
using MrCMS.Entities.Documents.Web;
using MrCMS.Entities.Widget;
using MrCMS.Models;
using NHibernate;
using MrCMS.Helpers;
using NHibernate.Transform;

namespace MrCMS.Services
{
    public class LayoutAreaService : ILayoutAreaService
    {
        private readonly ISession _session;

        public LayoutAreaService(ISession session)
        {
            _session = session;
        }
        public LayoutArea GetArea(Layout layout, string name)
        {
            return _session.QueryOver<LayoutArea>().Where(x => x.Layout == layout && x.AreaName == name).Fetch(
                area => area.Widgets).Eager.TransformUsing(Transformers.DistinctRootEntity).SingleOrDefault();
        }

        public void SaveArea(LayoutArea layoutArea)
        {
            _session.Transact(session =>
                                  {
                                      var layout = layoutArea.Layout;
                                      if (layout != null)
                                      {
                                          layout.LayoutAreas.Add(layoutArea);
                                          session.SaveOrUpdate(layout);
                                      }
                                      session.SaveOrUpdate(layoutArea);
                                  });
        }

        public LayoutArea GetArea(int layoutAreaId)
        {
            return _session.Get<LayoutArea>(layoutAreaId);
        }

        public void DeleteArea(LayoutArea area)
        {
            _session.Transact(session =>
                                  {
                                      if (area.Layout != null)
                                          area.Layout.LayoutAreas.Remove(area); //required to clear cache
                                      session.Delete(area);
                                  });
        }

        public void SetWidgetOrders(string orders)
        {
            _session.Transact(session =>
                                  {
                                      var widgetIds = orders.GetIntList();
                                      foreach (var widgetId in widgetIds)
                                      {
                                          var widget = session.Get<Widget>(widgetId);
                                          widget.DisplayOrder = widgetIds.IndexOf(widgetId);
                                          session.SaveOrUpdate(widget);
                                      }
                                  });
        }

        public void SetWidgetForPageOrder(WidgetPageOrder order)
        {
            _session.Transact(session =>
                                  {

                                      var layoutArea = _session.Get<LayoutArea>(order.LayoutAreaId);
                                      var webpage = _session.Get<Webpage>(order.WebpageId);
                                      var widget = _session.Get<Widget>(order.WidgetId);

                                      var widgetSort =
                                          _session.QueryOver<PageWidgetSort>().Where(
                                              sort =>
                                              sort.LayoutArea == layoutArea && sort.Webpage == webpage &&
                                              sort.Widget == widget).SingleOrDefault() ?? new PageWidgetSort
                                                                                              {
                                                                                                  LayoutArea =
                                                                                                      layoutArea,
                                                                                                  Webpage = webpage,
                                                                                                  Widget = widget
                                                                                              };
                                      widgetSort.Order = order.Order;
                                      if (!layoutArea.PageWidgetSorts.Contains(widgetSort))
                                          layoutArea.PageWidgetSorts.Add(widgetSort);
                                      if (!webpage.PageWidgetSorts.Contains(widgetSort))
                                          webpage.PageWidgetSorts.Add(widgetSort);
                                      if (!widget.PageWidgetSorts.Contains(widgetSort))
                                          widget.PageWidgetSorts.Add(widgetSort);
                                      session.SaveOrUpdate(widgetSort);

                                  });
        }
    }
}