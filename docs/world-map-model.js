(function (root, factory) {
  "use strict";
  var api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.LODWorldMapModel = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  function create(data) {
    var rawMaps = data["맵"] || {};
    var out = {}, inn = {}, clusterById = {};
    var maps = Object.keys(rawMaps).map(function (id) {
      var raw = rawMaps[id];
      return { id: String(id), name: raw["이름"], width: raw["가로"], height: raw["세로"] };
    });
    var byId = {};
    maps.forEach(function (map) { byId[map.id] = map; out[map.id] = []; inn[map.id] = []; });

    (data["간선"] || []).forEach(function (edge) {
      var from = String(edge[0]), to = String(edge[1]), count = edge[2];
      if (!byId[from] || !byId[to]) return;
      out[from].push({ id: to, count: count });
      inn[to].push({ id: from, count: count });
    });

    var clusters = (data["덩어리"] || []).map(function (rawIds, index) {
      var ids = rawIds.map(String);
      ids.forEach(function (id) { clusterById[id] = index; });
      var warpCount = ids.reduce(function (sum, id) {
        return sum + out[id].reduce(function (n, route) { return n + route.count; }, 0);
      }, 0);
      return { index: index, ids: ids, mapCount: ids.length, warpCount: warpCount };
    });

    function routeHasReverse(from, to) {
      return out[to].some(function (route) { return route.id === from; });
    }

    function connections(id) {
      id = String(id);
      return {
        incoming: inn[id].map(function (route) {
          return { id: route.id, count: route.count, reciprocal: routeHasReverse(route.id, id) };
        }),
        outgoing: out[id].map(function (route) {
          return { id: route.id, count: route.count, reciprocal: routeHasReverse(id, route.id) };
        })
      };
    }

    function status(id) {
      var links = connections(id);
      if (!links.incoming.length && !links.outgoing.length) return "isolated";
      if (!links.incoming.length) return "outgoing";
      if (!links.outgoing.length) return "incoming";
      return "both";
    }

    function search(query) {
      var q = String(query || "").trim().toLocaleLowerCase("ko");
      if (!q) return maps.slice();
      return maps.filter(function (map) {
        return map.id.indexOf(q) >= 0 || map.name.toLocaleLowerCase("ko").indexOf(q) >= 0;
      }).sort(function (a, b) {
        var ae = a.id === q || a.name.toLocaleLowerCase("ko") === q;
        var be = b.id === q || b.name.toLocaleLowerCase("ko") === q;
        return Number(be) - Number(ae) || a.name.localeCompare(b.name, "ko");
      });
    }

    return {
      maps: maps,
      byId: byId,
      clusters: clusters,
      clusterById: clusterById,
      connections: connections,
      status: status,
      search: search
    };
  }

  return { create: create };
});
