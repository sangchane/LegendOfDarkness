(function (root, factory) {
  "use strict";
  var api = factory();
  if (typeof module === "object" && module.exports) { module.exports = api; }
  root.LodDashboardModel = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";
  var views = Object.freeze(["overview", "system", "flows", "delivery", "knowledge", "operations", "prototypes"]);
  function normalizeView(value) { return views.indexOf(value) >= 0 ? value : "overview"; }
  function filterKnowledge(entries, category, query) {
    var selected = category || "all";
    var needle = String(query || "").trim().toLocaleLowerCase("ko");
    return entries.filter(function (entry) {
      var matchesCategory = selected === "all" || entry.category === selected;
      var haystack = [entry.title, entry.label, entry.summary, entry.source, entry.next].join(" ").toLocaleLowerCase("ko");
      return matchesCategory && (!needle || haystack.indexOf(needle) >= 0);
    });
  }
  function findFlow(flows, id) { return flows.find(function (flow) { return flow.id === id; }) || flows[0] || null; }
  return Object.freeze({ views: views, normalizeView: normalizeView, filterKnowledge: filterKnowledge, findFlow: findFlow });
});
