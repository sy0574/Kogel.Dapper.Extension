﻿using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Kogel.Dapper.Extension.Core.Interfaces;
using Kogel.Dapper.Extension.Oracle.Extension;

namespace Kogel.Dapper.Extension.Oracle
{
    public class OracleSqlProvider : SqlProvider
    {
        private readonly static string OpenQuote = "\"";
        private readonly static string CloseQuote = "\"";
        private readonly static char ParameterPrefix = ':';
        private IResolveExpression ResolveExpression;
        public OracleSqlProvider()
        {
            ProviderOption = new ProviderOption(OpenQuote, CloseQuote, ParameterPrefix);
            ResolveExpression = new ResolveExpression(this);
        }

        public sealed override IProviderOption ProviderOption { get; set; }

        public override SqlProvider FormatGet<T>()
        {
            var selectSql = ResolveExpression.ResolveSelect(null);

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref selectSql);

			var groupSql = ResolveExpression.ResolveGroupBy();

			var havingSql = ResolveExpression.ResolveHaving();

			var orderbySql = ResolveExpression.ResolveOrderBy();

            SqlString = $@"SELECT T.* FROM( 
                            {selectSql}
                            {fromTableSql} {joinSql}
                            {whereSql}
                            {groupSql}
                            {havingSql}
                            {orderbySql}
                            ) T
                            WHERE ROWNUM<=1";

            return this;
        }

        public override SqlProvider FormatToList<T>()
        {
            var selectSql = ResolveExpression.ResolveSelect(null);

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref selectSql);

			var groupSql = ResolveExpression.ResolveGroupBy();

			var havingSql = ResolveExpression.ResolveHaving();

			var orderbySql = ResolveExpression.ResolveOrderBy();

            SqlString = $"{selectSql} {fromTableSql} {joinSql} {whereSql} {groupSql} {havingSql} {orderbySql}";

            return this;
        }

        public override SqlProvider FormatToPageList<T>(int pageIndex, int pageSize)
        {
            var orderbySql = ResolveExpression.ResolveOrderBy();

            var selectSql = ResolveExpression.ResolveSelect(null);

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref selectSql);

			var groupSql = ResolveExpression.ResolveGroupBy();

			var havingSql = ResolveExpression.ResolveHaving();

			SqlString = $@" SELECT T2.* FROM(
                            SELECT T.*,ROWNUM ROWNUMS FROM (
                            SELECT 
                            {(new Regex("SELECT").Replace(selectSql, "", 1))}
                            {fromTableSql} {joinSql} {whereSql} {groupSql} {havingSql} {orderbySql}
                            ) T 
                            )T2
                            WHERE ROWNUMS BETWEEN {((pageIndex - 1) * pageSize) + 1} and {pageIndex * pageSize}";
            return this;
        }

        public override SqlProvider FormatCount()
        {
            var selectSql = "SELECT COUNT(1)";

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			string noneSql = "";
			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref noneSql);

			SqlString = $"{selectSql} {fromTableSql} {joinSql} {whereSql} ";
			return this;
        }

        public override SqlProvider FormatDelete()
        {
            var fromTableSql = ProviderOption.CombineFieldName(FormatTableName(false, false).Trim());

			ProviderOption.IsAsName = false;

			var whereSql = ResolveExpression.ResolveWhereList();
			SqlString = $"DELETE {fromTableSql} {whereSql}";
            return this;
        }

        public override SqlProvider FormatInsert<T>(T entity, string[] excludeFields)
		{
            var fromTableSql = ProviderOption.CombineFieldName(FormatTableName(false, false).Trim());
            var paramsAndValuesSql = FormatInsertParamsAndValues(entity);
            SqlString = $"INSERT INTO {fromTableSql} ({paramsAndValuesSql[0]}) VALUES({paramsAndValuesSql[1]})";
            return this;
        }

        public override SqlProvider FormatInsertIdentity<T>(T entity, string[] excludeFields)
		{
            var fromTableSql = ProviderOption.CombineFieldName(FormatTableName(false, false).Trim());
            var paramsAndValuesSql = FormatInsertParamsAndValues(entity);
            SqlString = $"INSERT INTO {fromTableSql} ({paramsAndValuesSql[0]}) VALUES({paramsAndValuesSql[1]}) SELECT @@IDENTITY";
            return this;
        }

        public override SqlProvider FormatUpdate<T>(Expression<Func<T, T>> updateExpression)
        {
            var update = ResolveExpression.ResolveUpdate(updateExpression);

            var fromTableSql = ProviderOption.CombineFieldName(FormatTableName(false, false).Trim());

			ProviderOption.IsAsName = false;

			var whereSql = ResolveExpression.ResolveWhereList();
			Params.AddDynamicParams(update.Param);

            SqlString = $"UPDATE {fromTableSql} {update.SqlCmd} {whereSql}";

            return this;
        }

        public override SqlProvider FormatUpdate<T>(T entity, string[] excludeFields, bool isBatch = false)
        {
			var update = ResolveExpression.ResolveUpdates<T>(entity, Params, excludeFields);
            var fromTableSql = ProviderOption.CombineFieldName(FormatTableName(false, false).Trim());

			ProviderOption.IsAsName = false;

			var whereSql = ResolveExpression.ResolveWhereList();
			//如果不存在条件，就用主键作为条件
			if (!isBatch)
				if (whereSql.Trim().Equals("WHERE 1=1"))
					whereSql += GetIdentityWhere(entity, Params);

			SqlString = $"UPDATE {fromTableSql} {update} {whereSql}";
            return this;
        }

        public override SqlProvider FormatSum(LambdaExpression sumExpression)
        {
            var selectSql = ResolveExpression.ResolveSum(sumExpression);

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			string noneSql = "";
			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref noneSql);

			SqlString = $"{selectSql} {fromTableSql}{joinSql} {whereSql} ";

            return this;
        }

        public override SqlProvider FormatMin(LambdaExpression minExpression)
        {
            var selectSql = ResolveExpression.ResolveMin(minExpression);

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			string noneSql = "";
			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref noneSql);

			SqlString = $"{selectSql} {fromTableSql}{joinSql} {whereSql} ";

            return this;
        }

        public override SqlProvider FormatMax(LambdaExpression maxExpression)
        {
            var selectSql = ResolveExpression.ResolveMax(maxExpression);

            var fromTableSql = FormatTableName();

			var whereSql = ResolveExpression.ResolveWhereList();

			string noneSql = "";
			var joinSql = ResolveExpression.ResolveJoinSql(JoinList, ref noneSql);

			SqlString = $"{selectSql} {fromTableSql}{joinSql} {whereSql} ";

            return this;
        }


        public override SqlProvider FormatUpdateSelect<T>(Expression<Func<T, T>> updator)
        {
            var update = ResolveExpression.ResolveUpdate(updator);

            var fromTableSql = ProviderOption.CombineFieldName(FormatTableName(false, false).Trim());
            var selectSql = ResolveExpression.ResolveSelectOfUpdate(EntityCache.QueryEntity(typeof(T)), Context.Set.SelectExpression);

			ProviderOption.IsAsName = false;

			var whereSql = ResolveExpression.ResolveWhereList();
			Params.AddDynamicParams(update.Param);

            SqlString = $"UPDATE {fromTableSql} {update.SqlCmd} {selectSql} {whereSql}";

            return this;
        }

		public override SqlProvider CreateNew()
		{
			return new OracleSqlProvider();
		}
	}
}
