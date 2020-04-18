# Qbcode.QuartZ
Bumblebee网关的QuartZ扩展插件，目前只支持定时调用接口

# 在使用之前
插件包括了一些新的功能  请使用  https://github.com/wwping/Bumblebee 这个修改过的库，主要功能会保持原项目同步更新


## 使用方法

### 1，加载插件
```
g = new Gateway();
.....省略省略
g.LoadPlugin(
                 typeof(Qbcode.QuartZ.Plugin).Assembly
               );
```
### 2，启用并配置插件
```
[
    {
        //调用的url
        "Url": "/api/xxx/xxx",
        //corn表达式
        "Corn": "0 0 0 * * ? *"
    }
]
```
### 3，关于corn表达式
- 规则1 七个部分  秒 分 时 日 月 星期 年  
- 规则2  * 表示任意值 ? 为不关注这个值，且 ? 只能出现在 星期 和 月份，因为月份和星期会互相影响，所以必定有一个是 ? 
- 示例1 0 0 0 * * ? * 表示每年 每月 每日 0时 0分 0秒
- 更多内容请自行 baidu或者google学习
